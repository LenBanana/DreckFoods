using System.Net;
using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.DTOs.Enums;
using FoodDbAPI.Models;
using FoodDbAPI.Models.Fddb;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class FoodService(
    FoodDbContext context,
    ILogger<FoodService> logger,
    IFddbScrapingService scrapingService) : IFoodService
{
    public async Task<FoodSearchResponse> SearchFoodsAsync(
        string query,
        int page = 1,
        int pageSize = 20,
        FoodSortBy sortBy = FoodSortBy.Name,
        SortDirection sortDirection = SortDirection.Ascending)
    {
        // First, search in the database
        var dbResults = await SearchFoodsInDatabaseAsync(query, page, pageSize, sortBy, sortDirection);

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
                // Save scraped foods to database for future searches
                await SaveScrapedFoodsToDatabaseAsync(scrapedFoods);

                // Convert scraped foods to DTOs and return
                var foodDtos = scrapedFoods.Select(MapScrapedFoodToDto).ToList();

                return new FoodSearchResponse
                {
                    Foods = ApplySortingToScrapedFoods(foodDtos, sortBy, sortDirection)
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

        var totalCount = await searchQuery.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var orderedQuery = ApplySorting(searchQuery, sortBy, sortDirection);

        var foods = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FoodSearchDto
            {
                Id = f.Id,
                Name = WebUtility.HtmlDecode(f.Name),
                Url = f.Url,
                Description = f.Description,
                ImageUrl = f.ImageUrl,
                Brand = f.Brand,
                Tags = f.Tags,
                Nutrition = f.Nutrition.ToNutritionInfo()
            })
            .ToListAsync();

        return new FoodSearchResponse
        {
            Foods = foods,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    private async Task SaveScrapedFoodsToDatabaseAsync(List<FddbFoodImportDTO> scrapedFoods)
    {
        try
        {
            var existingUrls = await context.FddbFoods
                .Where(f => scrapedFoods.Select(s => s.Url).Contains(f.Url))
                .Select(f => f.Url)
                .ToListAsync();

            var newFoods = scrapedFoods
                .Where(f => !existingUrls.Contains(f.Url))
                .Select(MapImportDtoToEntity)
                .ToList();

            if (newFoods.Count > 0)
            {
                context.FddbFoods.AddRange(newFoods);
                await context.SaveChangesAsync();

                logger.LogInformation("Saved {Count} new foods to database", newFoods.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving scraped foods to database");
        }
    }

    private static FddbFood MapImportDtoToEntity(FddbFoodImportDTO dto)
    {
        return new FddbFood
        {
            Name = dto.Name,
            Url = dto.Url,
            Description = dto.Description,
            ImageUrl = dto.ImageUrl,
            Brand = dto.Brand,
            Ean = dto.Ean,
            Tags = dto.Tags,
            Nutrition = FddbFoodNutrition.FromNutritionInfo(dto.Nutrition)
        };
    }

    private static FoodSearchDto MapScrapedFoodToDto(FddbFoodImportDTO dto)
    {
        return new FoodSearchDto
        {
            Id = 0,
            Name = WebUtility.HtmlDecode(dto.Name),
            Url = dto.Url,
            Description = dto.Description,
            ImageUrl = dto.ImageUrl,
            Brand = dto.Brand,
            Tags = dto.Tags,
            Nutrition = dto.Nutrition
        };
    }

    private static List<FoodSearchDto> ApplySortingToScrapedFoods(
        List<FoodSearchDto> foods,
        FoodSortBy sortBy,
        SortDirection sortDirection)
    {
        return sortBy switch
        {
            FoodSortBy.Name => sortDirection == SortDirection.Ascending
                ? foods.OrderBy(f => f.Name).ToList()
                : foods.OrderByDescending(f => f.Name).ToList(),
            FoodSortBy.Brand => sortDirection == SortDirection.Ascending
                ? foods.OrderBy(f => f.Brand).ToList()
                : foods.OrderByDescending(f => f.Brand).ToList(),
            FoodSortBy.Calories => sortDirection == SortDirection.Ascending
                ? foods.OrderBy(f => f.Nutrition.Calories.Value).ToList()
                : foods.OrderByDescending(f => f.Nutrition.Calories.Value).ToList(),
            FoodSortBy.Protein => sortDirection == SortDirection.Ascending
                ? foods.OrderBy(f => f.Nutrition.Protein.Value).ToList()
                : foods.OrderByDescending(f => f.Nutrition.Protein.Value).ToList(),
            FoodSortBy.Carbs => sortDirection == SortDirection.Ascending
                ? foods.OrderBy(f => f.Nutrition.Carbohydrates.Total.Value).ToList()
                : foods.OrderByDescending(f => f.Nutrition.Carbohydrates.Total.Value).ToList(),
            FoodSortBy.Fat => sortDirection == SortDirection.Ascending
                ? foods.OrderBy(f => f.Nutrition.Fat.Value).ToList()
                : foods.OrderByDescending(f => f.Nutrition.Fat.Value).ToList(),
            _ => foods.OrderBy(f => f.Name).ToList()
        };
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
                Description = fe.FddbFood.Description,
                ImageUrl = fe.FddbFood.ImageUrl,
                Brand = fe.FddbFood.Brand,
                Tags = fe.FddbFood.Tags,
                Nutrition = fe.FddbFood.Nutrition.ToNutritionInfo()
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
            (current, currentTerm) => current.Where(f =>
                EF.Functions.ILike(f.Name, $"%{currentTerm}%") || EF.Functions.ILike(f.Brand, $"%{currentTerm}%") ||
                (f.Ean != null && EF.Functions.ILike(f.Ean, $"%{currentTerm}%")) ||
                EF.Functions.ILike(f.Description, $"%{currentTerm}%")));
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

    public async Task<FoodEntryDto> AddFoodEntryAsync(int userId, CreateFoodEntryRequest request)
    {
        var food = await GetFoodWithNutritionAsync(request.FddbFoodId);

        var foodEntry = CreateFoodEntryFromFood(food, request.GramsConsumed);
        foodEntry.UserId = userId;
        foodEntry.FddbFoodId = food.Id;
        foodEntry.ConsumedAt = request.ConsumedAt.ToUniversalTime();
        foodEntry.CreatedAt = DateTime.UtcNow;

        context.FoodEntries.Add(foodEntry);
        await context.SaveChangesAsync();

        logger.LogInformation("Food entry added for user {UserId}: {FoodName} - {Grams}g",
            userId, food.Name, request.GramsConsumed);

        return MapToFoodEntryDto(foodEntry);
    }

    public async Task<FoodEntryDto> EditFoodEntryAsync(int userId, EditFoodEntryRequest request)
    {
        var entry = await context.FoodEntries
            .FirstOrDefaultAsync(f => f.Id == request.FddbFoodId && f.UserId == userId);

        if (entry == null)
            throw new ArgumentException("Food entry not found");

        var food = await GetFoodWithNutritionAsync(entry.FddbFoodId);

        UpdateFoodEntryFromFood(entry, food, request.GramsConsumed);
        entry.CreatedAt = DateTime.UtcNow;

        context.FoodEntries.Update(entry);
        await context.SaveChangesAsync();

        logger.LogInformation("Food entry edited for user {UserId}: {FoodName} - {Grams}g",
            userId, food.Name, request.GramsConsumed);

        return MapToFoodEntryDto(entry);
    }

    // Helper methods
    private async Task<FddbFood> GetFoodWithNutritionAsync(int foodId)
    {
        var food = await context.FddbFoods
            .Include(f => f.Nutrition)
            .FirstOrDefaultAsync(f => f.Id == foodId);

        if (food == null)
            throw new ArgumentException("Food not found");

        return food;
    }

    private FoodEntry CreateFoodEntryFromFood(FddbFood food, double gramsConsumed)
    {
        var entry = new FoodEntry();
        UpdateFoodEntryFromFood(entry, food, gramsConsumed);
        return entry;
    }

    private void UpdateFoodEntryFromFood(FoodEntry entry, FddbFood food, double gramsConsumed)
    {
        var multiplier = gramsConsumed / 100.0;
        var nutrition = food.Nutrition.ToNutritionInfo();

        entry.FoodName = WebUtility.HtmlDecode(food.Name);
        entry.FoodUrl = food.Url;
        entry.Brand = food.Brand;
        entry.ImageUrl = food.ImageUrl;
        entry.GramsConsumed = gramsConsumed;
        entry.Calories = nutrition.Calories.Value * multiplier;
        entry.Protein = nutrition.Protein.Value * multiplier;
        entry.Fat = nutrition.Fat.Value * multiplier;
        entry.Carbohydrates = nutrition.Carbohydrates.Total.Value * multiplier;
        entry.Fiber = nutrition.Fiber.Value * multiplier;
        entry.Sugar = nutrition.Carbohydrates.Sugar.Value * multiplier;
    }

    public async Task<List<FoodEntryDto>> GetFoodEntriesAsync(int userId, DateTime? date = null)
    {
        var query = context.FoodEntries.Where(f => f.UserId == userId);

        if (date.HasValue)
        {
            var startOfDay = date.Value.Date.ToUniversalTime();
            var endOfDay = startOfDay.AddDays(1);
            query = query.Where(f => f.ConsumedAt >= startOfDay && f.ConsumedAt < endOfDay);
        }

        var entries = await query
            .OrderByDescending(f => f.ConsumedAt)
            .ToListAsync();

        return entries.Select(MapToFoodEntryDto).ToList();
    }

    public async Task DeleteFoodEntryAsync(int userId, int entryId)
    {
        var entry = await context.FoodEntries
            .FirstOrDefaultAsync(f => f.Id == entryId && f.UserId == userId);

        if (entry == null)
        {
            throw new ArgumentException("Food entry not found");
        }

        context.FoodEntries.Remove(entry);
        await context.SaveChangesAsync();

        logger.LogInformation("Food entry deleted: {EntryId} for user {UserId}", entryId, userId);
    }

    public async Task<List<DailyTimelineDto>> GetTimelineAsync(int userId, DateTime startDate, DateTime endDate)
    {
        endDate = endDate.AddDays(1).AddTicks(-1);
        var foodEntries = await context.FoodEntries
            .Where(f => f.UserId == userId &&
                        f.ConsumedAt >= startDate &&
                        f.ConsumedAt <= endDate)
            .OrderBy(f => f.ConsumedAt)
            .ToListAsync();

        var weightEntries = await context.WeightEntries
            .Where(w => w.UserId == userId &&
                        w.RecordedAt >= startDate &&
                        w.RecordedAt <= endDate)
            .ToListAsync();

        var timeline = new List<DailyTimelineDto>();
        var currentDate = endDate.Date;

        while (currentDate >= startDate.Date)
        {
            var dayFoodEntries = foodEntries
                .Where(f => f.ConsumedAt.Date == currentDate)
                .ToList();

            var dayWeightEntry = weightEntries
                .FirstOrDefault(w => w.RecordedAt.Date == currentDate);

            var dailyData = new DailyTimelineDto
            {
                Date = currentDate,
                TotalCalories = dayFoodEntries.Sum(f => f.Calories),
                TotalProtein = dayFoodEntries.Sum(f => f.Protein),
                TotalFat = dayFoodEntries.Sum(f => f.Fat),
                TotalCarbohydrates = dayFoodEntries.Sum(f => f.Carbohydrates),
                TotalSugar = dayFoodEntries.Sum(f => f.Sugar),
                TotalFiber = dayFoodEntries.Sum(f => f.Fiber),
                FoodEntries = dayFoodEntries.Select(MapToFoodEntryDto).ToList(),
                WeightEntry = dayWeightEntry != null
                    ? new WeightEntryDto
                    {
                        Id = dayWeightEntry.Id,
                        Weight = dayWeightEntry.Weight,
                        RecordedAt = dayWeightEntry.RecordedAt,
                        CreatedAt = dayWeightEntry.CreatedAt
                    }
                    : null
            };

            timeline.Add(dailyData);
            currentDate = currentDate.AddDays(-1);
        }

        return timeline;
    }

    private static FoodEntryDto MapToFoodEntryDto(FoodEntry entry)
    {
        return new FoodEntryDto
        {
            Id = entry.Id,
            FoodName = WebUtility.HtmlDecode(entry.FoodName),
            FoodUrl = entry.FoodUrl,
            Brand = entry.Brand,
            ImageUrl = entry.ImageUrl,
            GramsConsumed = entry.GramsConsumed,
            Calories = entry.Calories,
            Protein = entry.Protein,
            Fat = entry.Fat,
            Carbohydrates = entry.Carbohydrates,
            Fiber = entry.Fiber,
            Sugar = entry.Sugar,
            ConsumedAt = entry.ConsumedAt,
            CreatedAt = entry.CreatedAt
        };
    }
}