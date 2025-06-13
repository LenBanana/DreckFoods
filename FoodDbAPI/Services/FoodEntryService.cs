using System.Net;
using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.DTOs.Base;
using FoodDbAPI.Models;
using FoodDbAPI.Models.Fddb;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class FoodEntryService(
    FoodDbContext context,
    ILogger<FoodEntryService> logger)
    : IFoodEntryService
{
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

        return FoodEntryDto.MapToFoodEntryDto(foodEntry);
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

        return FoodEntryDto.MapToFoodEntryDto(entry);
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

        return entries.Select(FoodEntryDto.MapToFoodEntryDto).ToList();
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

        // Set basic metadata
        entry.FoodName = WebUtility.HtmlDecode(food.Name);
        entry.FoodUrl = food.Url;
        entry.Brand = food.Brand;
        entry.ImageUrl = food.ImageUrl;
        entry.GramsConsumed = gramsConsumed;
        
        // Set base nutrition values
        entry.Calories = food.Nutrition.CaloriesValue;
        entry.Protein = food.Nutrition.ProteinValue;
        entry.Fat = food.Nutrition.FatValue;
        entry.Carbohydrates = food.Nutrition.CarbohydratesTotalValue;
        entry.Sugar = food.Nutrition.CarbohydratesSugarValue;
        entry.Fiber = food.Nutrition.FiberValue;
        entry.Caffeine = food.Nutrition.CaffeineValue;
        entry.Salt = food.Nutrition.SaltValue;
        
        // Apply the multiplier to all nutritional values at once
        entry.ApplyMultiplier(multiplier);
    }
}
