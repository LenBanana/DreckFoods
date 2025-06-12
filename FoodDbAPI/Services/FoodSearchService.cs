using System.Net;
using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.DTOs.Enums;
using FoodDbAPI.Models.Fddb;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class FoodSearchService(
    FoodDbContext context,
    ILogger<FoodSearchService> logger,
    IFddbScrapingService scrapingService)
    : IFoodSearchService
{
    public async Task<FoodSearchResponse> SearchFoodsAsync(
        string query,
        int? userId = null,
        int page = 1,
        int pageSize = 20,
        FoodSortBy sortBy = FoodSortBy.Name,
        SortDirection sortDirection = SortDirection.Ascending)
    {
        // First, search in the database
        var dbResults = await SearchFoodsInDatabaseAsync(query, userId, page, pageSize, sortBy, sortDirection);

        // If we have results or we're on a page other than the first, return the database results
        if (dbResults.Foods.Count > 0 || page > 1)
        {
            return dbResults;
        }

        logger.LogInformation("No database results found for '{Query}', attempting to scrape", query);

        try
        {
            // Fallback to scraping if no database results on first page
            var scrapedFoods = await scrapingService.FindFoodItemByNameAsync(query);

            if (scrapedFoods.Count > 0)
            {
                // Save scraped foods to database for future searches and get the saved entities with IDs
                var savedFoods = await SaveScrapedFoodsToDatabaseAsync(scrapedFoods);

                // Convert saved foods (with proper IDs) to DTOs and return
                var foodDtos = savedFoods.Select(FoodSearchDto.MapSavedFoodToDto).ToList();

                return new FoodSearchResponse
                {
                    Foods = FoodSearchDto.ApplySortingToScrapedFoods(foodDtos, sortBy, sortDirection)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList(),
                    TotalCount = foodDtos.Count,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)foodDtos.Count / pageSize)
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while scraping foods for query '{Query}'", query);
        }

        // Return empty results if both database and scraping failed
        return new FoodSearchResponse
        {
            Foods = [],
            TotalCount = 0,
            Page = page,
            PageSize = pageSize,
            TotalPages = 0
        };
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

        // Get total count for pagination
        var totalCount = await searchQuery.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        // Get the basic ordered query based on user's sort preference
        var orderedQuery = ApplySorting(searchQuery, sortBy, sortDirection);

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
                // Execute the full query (without pagination) to get all matching foods
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
                    TotalPages = (int)Math.Ceiling((double)combinedFoods.Count / pageSize)
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
            TotalPages = totalPages
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
