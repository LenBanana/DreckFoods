using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Models.Fddb;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class DataImportService(FoodDbContext context, ILogger<DataImportService> logger) : IDataImportService
{
    public async Task ImportFoodDataAsync(List<FddbFoodImportDTO> foods)
    {
        const int batchSize = 1000;
        var totalBatches = (int)Math.Ceiling((double)foods.Count / batchSize);

        logger.LogInformation("Starting import of {FoodCount} foods in {BatchCount} batches",
            foods.Count, totalBatches);

        for (var batch = 0; batch < totalBatches; batch++)
        {
            var batchFoods = foods.Skip(batch * batchSize).Take(batchSize).ToList();

            foreach (var dbFood in batchFoods.Select(food => new FddbFood
                     {
                         Name = food.Name,
                         Url = food.Url,
                         Description = food.Description,
                         ImageUrl = food.ImageUrl,
                         Brand = food.Brand,
                         Tags = food.Tags,
                         Ean = food.Ean,
                         Nutrition = FddbFoodNutrition.FromNutritionInfo(food.Nutrition)
                     }))
            {
                // Check if food already exists
                var existingFood = await context.FddbFoods
                    .FirstOrDefaultAsync(f => f.Url == dbFood.Url || f.Ean == dbFood.Ean);
                if (existingFood != null)
                {
                    existingFood.Ean = dbFood.Ean;
                    context.FddbFoods.Update(existingFood);
                }
                else
                {
                    await context.FddbFoods.AddAsync(dbFood);
                }
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