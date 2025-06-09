using System.Net;
using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Models.Fddb;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class DataImportService(FoodDbContext context, ILogger<DataImportService> logger) : IDataImportService
{
    public async Task ImportFoodDataAsync(List<FddbFoodImportDto> foods)
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
    
    /// <summary>
    /// Removes any ImageUrls that are not valid and fixes names and descriptions by HtmlDecoding them.
    /// </summary>
    public async Task<int> CleanupDataAsync()
    {
        var updatedCount = 0;
        foreach (var food in context.FddbFoods)
        {
            var updated = false;

            // Check and fix ImageUrl
            if (!string.IsNullOrWhiteSpace(food.ImageUrl) && !Uri.IsWellFormedUriString(food.ImageUrl, UriKind.Absolute))
            {
                food.ImageUrl = string.Empty;
                updated = true;
            }

            // HtmlDecode Name and Description
            if (!string.IsNullOrWhiteSpace(food.Name))
            {
                var decodedName = WebUtility.HtmlDecode(food.Name);
                if (decodedName != food.Name)
                {
                    food.Name = decodedName;
                    updated = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(food.Description))
            {
                var decodedDescription = WebUtility.HtmlDecode(food.Description);
                if (decodedDescription != food.Description)
                {
                    food.Description = decodedDescription;
                    updated = true;
                }
            }
            
            // HtmlDecode Tags
            if (food.Tags is { Count: > 0 })
            {
                for (var i = 0; i < food.Tags.Count; i++)
                {
                    var decodedTag = WebUtility.HtmlDecode(food.Tags[i]);
                    if (decodedTag == food.Tags[i]) continue;
                    food.Tags[i] = decodedTag;
                    updated = true;
                }
            }
            
            // HtmlDecode Brand
            if (!string.IsNullOrWhiteSpace(food.Brand))
            {
                var decodedBrand = WebUtility.HtmlDecode(food.Brand);
                if (decodedBrand != food.Brand)
                {
                    food.Brand = decodedBrand;
                    updated = true;
                }
            }

            if (!updated) continue;
            context.FddbFoods.Update(food);
            updatedCount++;
        }

        await context.SaveChangesAsync();
        return updatedCount;
    }
}