using FoodDbAPI.Data;
using FoodDbAPI.Models.Fddb;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class DataImportService(FoodDbContext context, ILogger<DataImportService> logger) : IDataImportService
{
    public async Task ImportFoodDataAsync(List<FddbFood> foods)
    {
        const int batchSize = 1000;
        var totalBatches = (int)Math.Ceiling((double)foods.Count / batchSize);

        logger.LogInformation("Starting import of {FoodCount} foods in {BatchCount} batches",
            foods.Count, totalBatches);

        for (var batch = 0; batch < totalBatches; batch++)
        {
            var batchFoods = foods.Skip(batch * batchSize).Take(batchSize).ToList();

            foreach (var food in batchFoods)
            {
                var dbFood = new FddbFood
                {
                    Name = food.Name,
                    Url = food.Url,
                    Description = food.Description,
                    ImageUrl = food.ImageUrl,
                    Brand = food.Brand,
                    Tags = food.Tags
                };

                var nutrition = new FddbFoodNutrition
                {
                    KilojoulesValue = food.Nutrition.KilojoulesValue,
                    KilojoulesUnit = food.Nutrition.KilojoulesUnit,
                    CaloriesValue = food.Nutrition.CaloriesValue,
                    CaloriesUnit = food.Nutrition.CaloriesUnit,
                    ProteinValue = food.Nutrition.ProteinValue,
                    ProteinUnit = food.Nutrition.ProteinUnit,
                    FatValue = food.Nutrition.FatValue,
                    FatUnit = food.Nutrition.FatUnit,
                    CarbohydratesTotalValue = food.Nutrition.CarbohydratesTotalValue,
                    CarbohydratesTotalUnit = food.Nutrition.CarbohydratesTotalUnit,
                    CarbohydratesSugarValue = food.Nutrition.CarbohydratesSugarValue,
                    CarbohydratesSugarUnit = food.Nutrition.CarbohydratesSugarUnit,
                    CarbohydratesPolyolsValue = food.Nutrition.CarbohydratesPolyolsValue,
                    CarbohydratesPolyolsUnit = food.Nutrition.CarbohydratesPolyolsUnit,
                    FiberValue = food.Nutrition.FiberValue,
                    FiberUnit = food.Nutrition.FiberUnit,
                    SaltValue = food.Nutrition.SaltValue,
                    SaltUnit = food.Nutrition.SaltUnit,
                    IronValue = food.Nutrition.IronValue,
                    IronUnit = food.Nutrition.IronUnit,
                    ZincValue = food.Nutrition.ZincValue,
                    ZincUnit = food.Nutrition.ZincUnit,
                    MagnesiumValue = food.Nutrition.MagnesiumValue,
                    MagnesiumUnit = food.Nutrition.MagnesiumUnit,
                    ChlorideValue = food.Nutrition.ChlorideValue,
                    ChlorideUnit = food.Nutrition.ChlorideUnit,
                    ManganeseValue = food.Nutrition.ManganeseValue,
                    ManganeseUnit = food.Nutrition.ManganeseUnit,
                    SulfurValue = food.Nutrition.SulfurValue,
                    SulfurUnit = food.Nutrition.SulfurUnit,
                    PotassiumValue = food.Nutrition.PotassiumValue,
                    PotassiumUnit = food.Nutrition.PotassiumUnit,
                    CalciumValue = food.Nutrition.CalciumValue,
                    CalciumUnit = food.Nutrition.CalciumUnit,
                    PhosphorusValue = food.Nutrition.PhosphorusValue,
                    PhosphorusUnit = food.Nutrition.PhosphorusUnit,
                    CopperValue = food.Nutrition.CopperValue,
                    CopperUnit = food.Nutrition.CopperUnit,
                    FluorideValue = food.Nutrition.FluorideValue,
                    FluorideUnit = food.Nutrition.FluorideUnit,
                    IodineValue = food.Nutrition.IodineValue,
                    IodineUnit = food.Nutrition.IodineUnit
                };

                dbFood.Nutrition = nutrition;
                context.FddbFoods.Add(dbFood);
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Imported batch {BatchNumber}/{TotalBatches} ({FoodCount} foods)",
                batch + 1, totalBatches, batchFoods.Count);
        }

        logger.LogInformation("Food data import completed successfully");
    }

    public async Task<int> GetFoodCountAsync()
    {
        return await context.FddbFoods.CountAsync();
    }
}