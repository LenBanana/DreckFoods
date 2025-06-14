using System.Net;
using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.DTOs.Enums;
using FoodDbAPI.Models.Fddb;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FoodDbAPI.Services;

public class FoodSearchService(
    FoodDbContext context,
    ILogger<FoodSearchService> logger,
    IConfiguration configuration,
    IFddbScrapingService scrapingService)
    : IFoodSearchService
{
    private readonly int _maxSearchResults = configuration.GetValue("MaxSearchResults", 10000);

    public async Task<FoodSearchResponse> SearchFoodsAsync(
        string query,
        int? userId = null,
        int page = 1,
        int pageSize = 20,
        FoodSortBy sortBy = FoodSortBy.Name,
        SortDirection sortDirection = SortDirection.Ascending)
    {
        var forceScrape = false;
        
        // Get command from configuration
        var forceScrapeCommand = configuration["ForceScrapeCommand"] ?? "+fscrape";
        
        // Check if the query contains "+fscrape" and remove it from the search query
        if (query.EndsWith(forceScrapeCommand, StringComparison.OrdinalIgnoreCase))
        {
            query = query[..^forceScrapeCommand.Length].Trim();
            forceScrape = true;
            logger.LogInformation("Force scrape option detected for query '{Query}'", query);
        }

        // First, search in the database
        var dbResults = await SearchFoodsInDatabaseAsync(query, userId, page, pageSize, sortBy, sortDirection);

        // If we have no database results or forceScrape is true, and we're on the first page, try to scrape
        if ((dbResults.Foods.Count != 0 && !forceScrape) || page != 1) return dbResults;
        
        logger.LogInformation("{Reason} for '{Query}', attempting to scrape", 
            dbResults.Foods.Count == 0 ? "No database results found" : "Force scrape requested",
            query);

        try
        {
            // Scrape foods from external source
            var scrapedFoods = await scrapingService.FindFoodItemByNameAsync(query);

            if (scrapedFoods.Count > 0)
            {
                // Save scraped foods to database for future searches and get the saved entities with IDs
                var savedFoods = await SaveScrapedFoodsToDatabaseAsync(scrapedFoods);

                // Convert saved foods (with proper IDs) to DTOs
                var scrapedFoodDtos = savedFoods.Select(FoodSearchDto.MapSavedFoodToDto).ToList();
                    
                // If forceScrape and we have database results, merge the results
                if (forceScrape && dbResults.Foods.Count > 0)
                {
                    // Create a HashSet of IDs from the database results to avoid duplicates
                    var dbFoodIds = dbResults.Foods.Select(f => f.Id).ToHashSet();
                        
                    // Add only new foods from scraped results
                    var uniqueScrapedFoods = scrapedFoodDtos
                        .Where(f => !dbFoodIds.Contains(f.Id))
                        .ToList();
                        
                    // Merge the two sets of results
                    var mergedFoods = dbResults.Foods.Concat(uniqueScrapedFoods).ToList();
                        
                    // Apply the user's sorting preference to the merged results
                    var sortedMergedFoods = FoodSearchDto.ApplySortingToScrapedFoods(
                        mergedFoods, sortBy, sortDirection);
                        
                    // Apply pagination to the merged results
                    var paginatedFoods = sortedMergedFoods
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();
                        
                    return new FoodSearchResponse
                    {
                        Foods = paginatedFoods,
                        TotalCount = sortedMergedFoods.Count,
                        Page = page,
                        PageSize = pageSize,
                        TotalPages = (int)Math.Ceiling((double)sortedMergedFoods.Count / pageSize)
                    };
                }
                // If we didn't have database results or didn't merge, return just the scraped results
                else if (dbResults.Foods.Count == 0)
                {
                    return new FoodSearchResponse
                    {
                        Foods = FoodSearchDto.ApplySortingToScrapedFoods(scrapedFoodDtos, sortBy, sortDirection)
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .ToList(),
                        TotalCount = scrapedFoodDtos.Count,
                        Page = page,
                        PageSize = pageSize,
                        TotalPages = (int)Math.Ceiling((double)scrapedFoodDtos.Count / pageSize)
                    };
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while scraping foods for query '{Query}'", query);
        }

        return dbResults;
    }

    private async Task<FoodSearchResponse> SearchFoodsInDatabaseAsync(
        string query,
        int? userId,
        int page,
        int pageSize,
        FoodSortBy sortBy,
        SortDirection sortDirection)
    {
        var searchQuery = context.FddbFoods
            .Include(f => f.Nutrition)
            .AsQueryable();

        // Apply search filters
        searchQuery = ApplySearchFilters(searchQuery, query);

        // Get an estimate of the total count, but limit it to our max value for performance
        var totalCount = await searchQuery.CountAsync();
        var isLimitReached = totalCount > _maxSearchResults;
        
        // If we're over the limit, log it
        if (isLimitReached)
        {
            logger.LogWarning("Search for '{Query}' returned over {MaxResults} results, limiting to prevent performance issues", 
                query, _maxSearchResults);
            totalCount = _maxSearchResults;
        }

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        // Get the basic ordered query based on user's sort preference
        var orderedQuery = ApplySorting(searchQuery, sortBy, sortDirection);
        
        // Apply the maximum result limit if needed
        if (isLimitReached)
        {
            orderedQuery = orderedQuery.Take(_maxSearchResults);
        }

        // If userId is provided, we'll get the user's previously eaten foods
        HashSet<int> previouslyEatenFoodIds = new();
        
        if (userId.HasValue)
        {
            previouslyEatenFoodIds = await context.FoodEntries
                .Where(fe => fe.UserId == userId.Value)
                .Select(fe => fe.FddbFoodId)
                .Distinct()
                .ToHashSetAsync();
            
            logger.LogInformation("Found {Count} previously eaten foods for user {UserId}", 
                previouslyEatenFoodIds.Count, userId.Value);
            
            if (previouslyEatenFoodIds.Count > 0)
            {
                // Execute the query with the limit in place
                var allFoodEntities = await orderedQuery.ToListAsync();
                
                // Separate into two groups
                var previouslyEatenFoods = allFoodEntities
                    .Where(f => previouslyEatenFoodIds.Contains(f.Id))
                    .ToList();
                
                var newFoods = allFoodEntities
                    .Where(f => !previouslyEatenFoodIds.Contains(f.Id))
                    .ToList();
                
                // Map both groups to DTOs
                var previouslyEatenDtos = previouslyEatenFoods.Select(f => new FoodSearchDto
                {
                    Id = f.Id,
                    Name = WebUtility.HtmlDecode(f.Name),
                    Url = f.Url,
                    Description = WebUtility.HtmlDecode(f.Description),
                    ImageUrl = f.ImageUrl,
                    Brand = f.Brand,
                    Tags = f.Tags,
                    Nutrition = f.Nutrition.ToNutritionInfo(),
                    PreviouslyEaten = true
                }).ToList();
                
                var newFoodDtos = newFoods.Select(f => new FoodSearchDto
                {
                    Id = f.Id,
                    Name = WebUtility.HtmlDecode(f.Name),
                    Url = f.Url,
                    Description = WebUtility.HtmlDecode(f.Description),
                    ImageUrl = f.ImageUrl,
                    Brand = f.Brand,
                    Tags = f.Tags,
                    Nutrition = f.Nutrition.ToNutritionInfo(),
                    PreviouslyEaten = false
                }).ToList();
                
                // Apply the user's sorting preference to each group separately
                var sortedPreviouslyEaten = FoodSearchDto.ApplySortingToScrapedFoods(
                    previouslyEatenDtos, sortBy, sortDirection);
                
                var sortedNewFoods = FoodSearchDto.ApplySortingToScrapedFoods(
                    newFoodDtos, sortBy, sortDirection);
                
                // Combine the two groups with previously eaten foods first
                var combinedFoods = sortedPreviouslyEaten.Concat(sortedNewFoods).ToList();
                
                // Apply pagination to the combined results
                var paginatedFoods = combinedFoods
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
                
                return new FoodSearchResponse
                {
                    Foods = paginatedFoods,
                    TotalCount = combinedFoods.Count,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)combinedFoods.Count / pageSize),
                    ResultsLimited = isLimitReached
                };
            }
        }

        var foodEntities = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var foods = foodEntities.Select(f => new FoodSearchDto
        {
            Id = f.Id,
            Name = WebUtility.HtmlDecode(f.Name),
            Url = f.Url,
            Description = WebUtility.HtmlDecode(f.Description),
            ImageUrl = f.ImageUrl,
            Brand = f.Brand,
            Tags = f.Tags,
            Nutrition = f.Nutrition.ToNutritionInfo(),
            PreviouslyEaten = previouslyEatenFoodIds.Contains(f.Id)
        }).ToList();

        return new FoodSearchResponse
        {
            Foods = foods,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            ResultsLimited = isLimitReached
        };
    }

    private async Task<List<FddbFood>> SaveScrapedFoodsToDatabaseAsync(List<FddbFoodImportDto> scrapedFoods)
    {
        try
        {
            var existingUrls = await context.FddbFoods
                .Where(f => scrapedFoods.Select(s => s.Url).Contains(f.Url))
                .Select(f => f.Url)
                .ToListAsync();

            var newFoods = scrapedFoods
                .Where(f => !existingUrls.Contains(f.Url))
                .Select(FddbFoodImportDto.MapImportDtoToEntity)
                .ToList();

            var savedFoods = new List<FddbFood>();

            if (newFoods.Count > 0)
            {
                context.FddbFoods.AddRange(newFoods);
                await context.SaveChangesAsync();
                savedFoods.AddRange(newFoods);

                logger.LogInformation("Saved {Count} new foods to database", newFoods.Count);
            }

            // Also get existing foods that were already in the database
            var existingFoods = await context.FddbFoods
                .Include(f => f.Nutrition)
                .Where(f => existingUrls.Contains(f.Url))
                .ToListAsync();

            savedFoods.AddRange(existingFoods);

            return savedFoods;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving scraped foods to database");
            return [];
        }
    }

    public async Task<FoodSearchResponse> GetPastEatenFoodsAsync(int userId, int page = 1, int pageSize = 20)
    {
        var allEntries = await context.FoodEntries
            .Where(fe => fe.UserId == userId)
            .Include(fe => fe.FddbFood)
            .ThenInclude(f => f.Nutrition)
            .OrderByDescending(fe => fe.ConsumedAt)
            .ToListAsync();

        var distinctFoods = allEntries
            .DistinctBy(fe => fe.FddbFood.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(fe => new FoodSearchDto
            {
                Id = fe.FddbFood.Id,
                Name = WebUtility.HtmlDecode(fe.FddbFood.Name),
                Url = fe.FddbFood.Url,
                Description = WebUtility.HtmlDecode(fe.FddbFood.Description),
                ImageUrl = fe.FddbFood.ImageUrl,
                Brand = fe.FddbFood.Brand,
                Tags = fe.FddbFood.Tags,
                Nutrition = fe.FddbFood.Nutrition.ToNutritionInfo(),
                PreviouslyEaten = true // Set this flag to true since these are all previously eaten foods
            })
            .ToList();

        var totalDistinctCount = allEntries.DistinctBy(fe => fe.FddbFood.Id).Count();
        var totalPages = (int)Math.Ceiling((double)totalDistinctCount / pageSize);

        return new FoodSearchResponse
        {
            Foods = distinctFoods,
            TotalCount = totalDistinctCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    private IQueryable<FddbFood> ApplySearchFilters(IQueryable<FddbFood> query, string searchQuery)
    {
        if (string.IsNullOrWhiteSpace(searchQuery))
            return query;

        var searchTerms = searchQuery.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .ToArray();

        return searchTerms.Aggregate(query,
            (current, term) => current.Where(f =>
                EF.Functions.ILike(f.Name, $"%{term}%") || EF.Functions.ILike(f.Brand, $"%{term}%") ||
                (f.Ean != null && EF.Functions.ILike(f.Ean, $"%{term}%")) ||
                EF.Functions.ILike(f.Description, $"%{term}%")));
    }

    private IQueryable<FddbFood> ApplySorting(
        IQueryable<FddbFood> query,
        FoodSortBy sortBy,
        SortDirection sortDirection)
    {
        var orderedQuery = sortBy switch
        {
            FoodSortBy.Name => sortDirection == SortDirection.Ascending
                ? query.OrderBy(f => f.Name)
                : query.OrderByDescending(f => f.Name),
            FoodSortBy.Brand => sortDirection == SortDirection.Ascending
                ? query.OrderBy(f => f.Brand)
                : query.OrderByDescending(f => f.Brand),
            FoodSortBy.Calories => sortDirection == SortDirection.Ascending
                ? query.OrderBy(f => f.Nutrition.CaloriesValue)
                : query.OrderByDescending(f => f.Nutrition.CaloriesValue),
            FoodSortBy.Protein => sortDirection == SortDirection.Ascending
                ? query.OrderBy(f => f.Nutrition.ProteinValue)
                : query.OrderByDescending(f => f.Nutrition.ProteinValue),
            FoodSortBy.Carbs => sortDirection == SortDirection.Ascending
                ? query.OrderBy(f => f.Nutrition.CarbohydratesTotalValue)
                : query.OrderByDescending(f => f.Nutrition.CarbohydratesTotalValue),
            FoodSortBy.Fat => sortDirection == SortDirection.Ascending
                ? query.OrderBy(f => f.Nutrition.FatValue)
                : query.OrderByDescending(f => f.Nutrition.FatValue),
            _ => query.OrderBy(f => f.Name)
        };

        return orderedQuery;
    }

    public async Task<List<string>> GetFoodCategoriesAsync()
    {
        var foods = await context.FddbFoods
            .Select(f => f.Tags) // Only select the tags to minimize data transfer
            .ToListAsync();

        var categories = foods
            .SelectMany(tags => tags)
            .Distinct()
            .OrderBy(tag => tag)
            .ToList();

        return categories;
    }

    public async Task<FoodSearchDto?> GetFoodByIdAsync(int foodId)
    {
        var food = await context.FddbFoods
            .Include(f => f.Nutrition)
            .FirstOrDefaultAsync(f => f.Id == foodId);

        if (food == null)
            return null;

        return new FoodSearchDto
        {
            Id = food.Id,
            Name = WebUtility.HtmlDecode(food.Name),
            Url = food.Url,
            Description = food.Description,
            ImageUrl = food.ImageUrl,
            Brand = food.Brand,
            Tags = food.Tags,
            Nutrition = food.Nutrition.ToNutritionInfo()
        };
    }
}
