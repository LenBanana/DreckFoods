using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Models.Fddb;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class FddbEditorService(FoodDbContext context) : IFddbEditorService
{
    public async Task<FoodSearchDto?> GetFoodByIdAsync(int id)
    {
        return FoodSearchDto.MapSavedFoodToDto(await context.FddbFoods
            .Include(f => f.Nutrition)
            .FirstOrDefaultAsync(f => f.Id == id));
    }

    public async Task<bool> UpdateFoodInfoAsync(int id, FddbFoodUpdateDTO? updateDto)
    {
        if (updateDto == null)
            return false;
        
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
        return true;
    }

    public async Task<bool> UpdateFoodNutritionAsync(int id, FddbFoodNutritionUpdateDTO? updateDto)
    {
        if (updateDto == null)
            return false;
        
        var nutrition = await context.FddbFoodNutritions
            .FirstOrDefaultAsync(n => n.FddbFoodId == id);
            
        if (nutrition == null)
            return false;

        // Update only non-null properties
        if (updateDto.KilojoulesValue.HasValue)
            nutrition.KilojoulesValue = updateDto.KilojoulesValue.Value;
        if (updateDto.KilojoulesUnit != null)
            nutrition.KilojoulesUnit = updateDto.KilojoulesUnit;

        if (updateDto.CaloriesValue.HasValue)
            nutrition.CaloriesValue = updateDto.CaloriesValue.Value;
        if (updateDto.CaloriesUnit != null)
            nutrition.CaloriesUnit = updateDto.CaloriesUnit;

        if (updateDto.ProteinValue.HasValue)
            nutrition.ProteinValue = updateDto.ProteinValue.Value;
        if (updateDto.ProteinUnit != null)
            nutrition.ProteinUnit = updateDto.ProteinUnit;

        if (updateDto.FatValue.HasValue)
            nutrition.FatValue = updateDto.FatValue.Value;
        if (updateDto.FatUnit != null)
            nutrition.FatUnit = updateDto.FatUnit;

        if (updateDto.CarbohydratesTotalValue.HasValue)
            nutrition.CarbohydratesTotalValue = updateDto.CarbohydratesTotalValue.Value;
        if (updateDto.CarbohydratesTotalUnit != null)
            nutrition.CarbohydratesTotalUnit = updateDto.CarbohydratesTotalUnit;

        if (updateDto.CarbohydratesSugarValue.HasValue)
            nutrition.CarbohydratesSugarValue = updateDto.CarbohydratesSugarValue.Value;
        if (updateDto.CarbohydratesSugarUnit != null)
            nutrition.CarbohydratesSugarUnit = updateDto.CarbohydratesSugarUnit;

        if (updateDto.CarbohydratesPolyolsValue.HasValue)
            nutrition.CarbohydratesPolyolsValue = updateDto.CarbohydratesPolyolsValue.Value;
        if (updateDto.CarbohydratesPolyolsUnit != null)
            nutrition.CarbohydratesPolyolsUnit = updateDto.CarbohydratesPolyolsUnit;

        if (updateDto.FiberValue.HasValue)
            nutrition.FiberValue = updateDto.FiberValue.Value;
        if (updateDto.FiberUnit != null)
            nutrition.FiberUnit = updateDto.FiberUnit;

        // Minerals
        if (updateDto.SaltValue.HasValue)
            nutrition.SaltValue = updateDto.SaltValue.Value;
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
        return true;
    }

    public async Task<bool> UpdateFoodCompleteAsync(int id, FddbFoodCompleteUpdateDTO? updateDto)
    {
        if (updateDto == null)
            return false;
        
        var food = await context.FddbFoods
            .Include(f => f.Nutrition)
            .FirstOrDefaultAsync(f => f.Id == id);
            
        if (food == null)
            return false;

        if (updateDto.FoodInfo != null)
            await UpdateFoodInfoAsync(id, updateDto.FoodInfo);

        if (updateDto.Nutrition != null)
            await UpdateFoodNutritionAsync(id, updateDto.Nutrition);

        return true;
    }
}
