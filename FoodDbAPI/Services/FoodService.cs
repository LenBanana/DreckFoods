using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Models;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class FoodService(FoodDbContext context, ILogger<FoodService> logger) : IFoodService
{
    public async Task<FoodSearchResponse> SearchFoodsAsync(string query, int page = 1, int pageSize = 20)
    {
        var searchQuery = context.FddbFoods
            .Include(f => f.Nutrition)
            .Where(f => f.Name.Contains(query) ||
                       f.Brand.Contains(query) ||
                       f.Description.Contains(query) ||
                       f.TagsJson.Contains(query));

        var totalCount = await searchQuery.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var foods = await searchQuery
            .OrderBy(f => f.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FoodSearchDto
            {
                Id = f.Id,
                Name = f.Name,
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
            Name = food.Name,
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
        // Get the food from database
        var food = await context.FddbFoods
            .Include(f => f.Nutrition)
            .FirstOrDefaultAsync(f => f.Id == request.FddbFoodId);

        if (food == null)
        {
            throw new ArgumentException("Food not found");
        }

        // Calculate nutrition values based on grams consumed
        var multiplier = request.GramsConsumed / 100.0; // Convert from per 100g to actual amount
        var nutrition = food.Nutrition.ToNutritionInfo();

        var foodEntry = new FoodEntry
        {
            UserId = userId,
            FoodName = food.Name,
            FoodUrl = food.Url,
            Brand = food.Brand,
            ImageUrl = food.ImageUrl,
            GramsConsumed = request.GramsConsumed,
            Calories = nutrition.Calories.Value * multiplier,
            Protein = nutrition.Protein.Value * multiplier,
            Fat = nutrition.Fat.Value * multiplier,
            Carbohydrates = nutrition.Carbohydrates.Total.Value * multiplier,
            Fiber = nutrition.Fiber.Value * multiplier,
            Sugar = nutrition.Carbohydrates.Sugar.Value * multiplier,
            ConsumedAt = request.ConsumedAt,
            CreatedAt = DateTime.UtcNow
        };

        context.FoodEntries.Add(foodEntry);
        await context.SaveChangesAsync();

        logger.LogInformation("Food entry added for user {UserId}: {FoodName} - {Grams}g", 
            userId, food.Name, request.GramsConsumed);

        return MapToFoodEntryDto(foodEntry);
    }

    public async Task<List<FoodEntryDto>> GetFoodEntriesAsync(int userId, DateTime? date = null)
    {
        var query = context.FoodEntries.Where(f => f.UserId == userId);

        if (date.HasValue)
        {
            var startOfDay = date.Value.Date;
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
        var currentDate = startDate.Date;

        while (currentDate <= endDate.Date)
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
                TotalFiber = dayFoodEntries.Sum(f => f.Fiber),
                FoodEntries = dayFoodEntries.Select(MapToFoodEntryDto).ToList(),
                WeightEntry = dayWeightEntry != null ? new WeightEntryDto
                {
                    Id = dayWeightEntry.Id,
                    Weight = dayWeightEntry.Weight,
                    RecordedAt = dayWeightEntry.RecordedAt,
                    CreatedAt = dayWeightEntry.CreatedAt
                } : null
            };

            timeline.Add(dailyData);
            currentDate = currentDate.AddDays(1);
        }

        return timeline;
    }

    private static FoodEntryDto MapToFoodEntryDto(FoodEntry entry)
    {
        return new FoodEntryDto
        {
            Id = entry.Id,
            FoodName = entry.FoodName,
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