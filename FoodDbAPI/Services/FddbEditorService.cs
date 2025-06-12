using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Models.Fddb;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class FddbEditorService(FoodDbContext context, ILogger<FddbEditorService> logger) : IFddbEditorService
{
    public async Task<FoodSearchDto?> GetFoodByIdAsync(int id)
    {
        return FoodSearchDto.MapSavedFoodToDto(await context.FddbFoods
            .Include(f => f.Nutrition)
            .FirstOrDefaultAsync(f => f.Id == id));
    }

    public async Task<bool> UpdateFoodInfoAsync(int id, FddbFoodUpdateDTO updateDto)
    {
        var food = await context.FddbFoods.FindAsync(id);
        if (food == null)
            return false;

        // Update only non-null properties
        if (updateDto.Name != null)
            food.Name = updateDto.Name;
        
        if (updateDto.Url != null)
            food.Url = updateDto.Url;
        
        if (updateDto.Description != null)
            food.Description = updateDto.Description;
        
        if (updateDto.ImageUrl != null)
            food.ImageUrl = updateDto.ImageUrl;
        
        if (updateDto.Brand != null)
            food.Brand = updateDto.Brand;
        
        if (updateDto.Ean != null)
            food.Ean = updateDto.Ean;
        
        if (updateDto.Tags != null)
            food.Tags = updateDto.Tags;

        await context.SaveChangesAsync();
        
        // Update user entries that reference this food for updated brand, image and name
        if (updateDto.Brand != null || updateDto.ImageUrl != null || updateDto.Name != null)
        {
            await UpdateUserEntriesMetadataAsync(id, food);
        }
        
        return true;
    }

    public async Task<bool> UpdateFoodNutritionAsync(int id, FddbFoodNutritionUpdateDTO updateDto)
    {
        var nutrition = await context.FddbFoodNutritions
            .FirstOrDefaultAsync(n => n.FddbFoodId == id);
            
        if (nutrition == null)
            return false;

        var nutritionValuesChanged = false;

        // Update only non-null properties
        if (updateDto.KilojoulesValue.HasValue)
        {
            nutrition.KilojoulesValue = updateDto.KilojoulesValue.Value;
            nutritionValuesChanged = true;
        }
        if (updateDto.KilojoulesUnit != null)
            nutrition.KilojoulesUnit = updateDto.KilojoulesUnit;

        if (updateDto.CaloriesValue.HasValue)
        {
            nutrition.CaloriesValue = updateDto.CaloriesValue.Value;
            nutritionValuesChanged = true;
        }
        if (updateDto.CaloriesUnit != null)
            nutrition.CaloriesUnit = updateDto.CaloriesUnit;

        if (updateDto.ProteinValue.HasValue)
        {
            nutrition.ProteinValue = updateDto.ProteinValue.Value;
            nutritionValuesChanged = true;
        }
        if (updateDto.ProteinUnit != null)
            nutrition.ProteinUnit = updateDto.ProteinUnit;

        if (updateDto.FatValue.HasValue)
        {
            nutrition.FatValue = updateDto.FatValue.Value;
            nutritionValuesChanged = true;
        }
        if (updateDto.FatUnit != null)
            nutrition.FatUnit = updateDto.FatUnit;

        if (updateDto.CarbohydratesTotalValue.HasValue)
        {
            nutrition.CarbohydratesTotalValue = updateDto.CarbohydratesTotalValue.Value;
            nutritionValuesChanged = true;
        }
        if (updateDto.CarbohydratesTotalUnit != null)
            nutrition.CarbohydratesTotalUnit = updateDto.CarbohydratesTotalUnit;

        if (updateDto.CarbohydratesSugarValue.HasValue)
        {
            nutrition.CarbohydratesSugarValue = updateDto.CarbohydratesSugarValue.Value;
            nutritionValuesChanged = true;
        }
        if (updateDto.CarbohydratesSugarUnit != null)
            nutrition.CarbohydratesSugarUnit = updateDto.CarbohydratesSugarUnit;

        if (updateDto.CarbohydratesPolyolsValue.HasValue)
        {
            nutrition.CarbohydratesPolyolsValue = updateDto.CarbohydratesPolyolsValue.Value;
            nutritionValuesChanged = true;
        }
        if (updateDto.CarbohydratesPolyolsUnit != null)
            nutrition.CarbohydratesPolyolsUnit = updateDto.CarbohydratesPolyolsUnit;

        if (updateDto.FiberValue.HasValue)
        {
            nutrition.FiberValue = updateDto.FiberValue.Value;
            nutritionValuesChanged = true;
        }
        if (updateDto.FiberUnit != null)
            nutrition.FiberUnit = updateDto.FiberUnit;

        // Minerals
        if (updateDto.SaltValue.HasValue)
        {
            nutrition.SaltValue = updateDto.SaltValue.Value;
            nutritionValuesChanged = true;
        }
        if (updateDto.SaltUnit != null)
            nutrition.SaltUnit = updateDto.SaltUnit;

        if (updateDto.IronValue.HasValue)
            nutrition.IronValue = updateDto.IronValue.Value;
        if (updateDto.IronUnit != null)
            nutrition.IronUnit = updateDto.IronUnit;

        if (updateDto.ZincValue.HasValue)
            nutrition.ZincValue = updateDto.ZincValue.Value;
        if (updateDto.ZincUnit != null)
            nutrition.ZincUnit = updateDto.ZincUnit;

        if (updateDto.MagnesiumValue.HasValue)
            nutrition.MagnesiumValue = updateDto.MagnesiumValue.Value;
        if (updateDto.MagnesiumUnit != null)
            nutrition.MagnesiumUnit = updateDto.MagnesiumUnit;

        if (updateDto.ChlorideValue.HasValue)
            nutrition.ChlorideValue = updateDto.ChlorideValue.Value;
        if (updateDto.ChlorideUnit != null)
            nutrition.ChlorideUnit = updateDto.ChlorideUnit;

        if (updateDto.ManganeseValue.HasValue)
            nutrition.ManganeseValue = updateDto.ManganeseValue.Value;
        if (updateDto.ManganeseUnit != null)
            nutrition.ManganeseUnit = updateDto.ManganeseUnit;

        if (updateDto.SulfurValue.HasValue)
            nutrition.SulfurValue = updateDto.SulfurValue.Value;
        if (updateDto.SulfurUnit != null)
            nutrition.SulfurUnit = updateDto.SulfurUnit;

        if (updateDto.PotassiumValue.HasValue)
            nutrition.PotassiumValue = updateDto.PotassiumValue.Value;
        if (updateDto.PotassiumUnit != null)
            nutrition.PotassiumUnit = updateDto.PotassiumUnit;

        if (updateDto.CalciumValue.HasValue)
            nutrition.CalciumValue = updateDto.CalciumValue.Value;
        if (updateDto.CalciumUnit != null)
            nutrition.CalciumUnit = updateDto.CalciumUnit;

        if (updateDto.PhosphorusValue.HasValue)
            nutrition.PhosphorusValue = updateDto.PhosphorusValue.Value;
        if (updateDto.PhosphorusUnit != null)
            nutrition.PhosphorusUnit = updateDto.PhosphorusUnit;

        if (updateDto.CopperValue.HasValue)
            nutrition.CopperValue = updateDto.CopperValue.Value;
        if (updateDto.CopperUnit != null)
            nutrition.CopperUnit = updateDto.CopperUnit;

        if (updateDto.FluorideValue.HasValue)
            nutrition.FluorideValue = updateDto.FluorideValue.Value;
        if (updateDto.FluorideUnit != null)
            nutrition.FluorideUnit = updateDto.FluorideUnit;

        if (updateDto.IodineValue.HasValue)
            nutrition.IodineValue = updateDto.IodineValue.Value;
        if (updateDto.IodineUnit != null)
            nutrition.IodineUnit = updateDto.IodineUnit;
        
        if (updateDto.CaffeineValue.HasValue)
            nutrition.CaffeineValue = updateDto.CaffeineValue.Value;
        if (updateDto.CaffeineUnit != null)
            nutrition.CaffeineUnit = updateDto.CaffeineUnit;

        await context.SaveChangesAsync();
        
        // If any nutritional values changed, update user entries
        if (nutritionValuesChanged)
        {
            await UpdateUserEntriesForFoodAsync(id);
        }
        
        return true;
    }

    public async Task<bool> UpdateFoodCompleteAsync(int id, FddbFoodCompleteUpdateDTO updateDto)
    {
        var food = await context.FddbFoods
            .Include(f => f.Nutrition)
            .FirstOrDefaultAsync(f => f.Id == id);
            
        if (food == null)
            return false;

        var foodInfoChanged = false;
        var nutritionValuesChanged = false;

        if (updateDto.FoodInfo != null)
        {
            // Check if metadata fields are being updated
            foodInfoChanged = updateDto.FoodInfo.Brand != null || 
                             updateDto.FoodInfo.ImageUrl != null || 
                             updateDto.FoodInfo.Name != null;
                             
            await UpdateFoodInfoAsync(id, updateDto.FoodInfo);
        }

        if (updateDto.Nutrition != null)
        {
            // Check if any nutrition values that require user entry updates are changing
            nutritionValuesChanged = updateDto.Nutrition.CaloriesValue.HasValue || 
                                    updateDto.Nutrition.ProteinValue.HasValue || 
                                    updateDto.Nutrition.FatValue.HasValue || 
                                    updateDto.Nutrition.CarbohydratesTotalValue.HasValue ||
                                    updateDto.Nutrition.CarbohydratesSugarValue.HasValue ||
                                    updateDto.Nutrition.FiberValue.HasValue ||
                                    updateDto.Nutrition.CaffeineValue.HasValue;
                                    
            await UpdateFoodNutritionAsync(id, updateDto.Nutrition);
        }

        // Only update user entries metadata if not already updated by other methods
        if (foodInfoChanged && !nutritionValuesChanged)
        {
            await UpdateUserEntriesMetadataAsync(id, food);
        }

        return true;
    }

    public async Task<int> UpdateUserEntriesForFoodAsync(int foodId)
    {
        var food = await context.FddbFoods
            .Include(f => f.Nutrition)
            .FirstOrDefaultAsync(f => f.Id == foodId);

        if (food == null || food.Nutrition == null)
        {
            logger.LogWarning("Attempted to update user entries for non-existent food with ID {FoodId}", foodId);
            return 0;
        }

        // Get all food entries that reference this food
        var entries = await context.FoodEntries
            .Where(e => e.FddbFoodId == foodId)
            .ToListAsync();

        if (!entries.Any())
        {
            logger.LogInformation("No user entries found for food ID {FoodId}", foodId);
            return 0;
        }

        var updatedCount = 0;

        foreach (var entry in entries)
        {
            // Update the calculated nutrition values based on grams consumed
            var ratio = entry.GramsConsumed / 100.0; 
            
            entry.Calories = food.Nutrition.CaloriesValue * ratio;
            entry.Protein = food.Nutrition.ProteinValue * ratio;
            entry.Fat = food.Nutrition.FatValue * ratio;
            entry.Carbohydrates = food.Nutrition.CarbohydratesTotalValue * ratio;
            entry.Sugar = food.Nutrition.CarbohydratesSugarValue * ratio;
            entry.Fiber = food.Nutrition.FiberValue * ratio;
            entry.Caffeine = food.Nutrition.CaffeineValue * ratio;
            
            // Also update metadata fields
            entry.FoodName = food.Name;
            entry.Brand = food.Brand;
            entry.ImageUrl = food.ImageUrl;
            entry.FoodUrl = food.Url;

            updatedCount++;
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Updated {Count} user entries for food ID {FoodId}", updatedCount, foodId);

        return updatedCount;
    }
    
    // Helper method to update only metadata in user entries
    private async Task<int> UpdateUserEntriesMetadataAsync(int foodId, FddbFood food)
    {
        var entries = await context.FoodEntries
            .Where(e => e.FddbFoodId == foodId)
            .ToListAsync();

        if (!entries.Any())
        {
            return 0;
        }

        var updatedCount = 0;

        foreach (var entry in entries)
        {
            // Update only metadata fields
            entry.FoodName = food.Name;
            entry.Brand = food.Brand;
            entry.ImageUrl = food.ImageUrl;
            entry.FoodUrl = food.Url;

            updatedCount++;
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Updated metadata for {Count} user entries for food ID {FoodId}", updatedCount, foodId);

        return updatedCount;
    }
}
