using System.Net;
using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Models.Fddb;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class DataImportService : IDataImportService
{
    private readonly FoodDbContext _context;
    private readonly ILogger<DataImportService> _logger;
    private readonly IFddbEditorService _fddbEditorService;

    public DataImportService(FoodDbContext context, ILogger<DataImportService> logger, IFddbEditorService fddbEditorService)
    {
        _context = context;
        _logger = logger;
        _fddbEditorService = fddbEditorService;
    }

    public async Task ImportFoodDataAsync(List<FddbFoodImportDto> foods)
    {
        const int batchSize = 1000;
        var totalBatches = (int)Math.Ceiling((double)foods.Count / batchSize);

        _logger.LogInformation("Starting import of {FoodCount} foods in {BatchCount} batches",
            foods.Count, totalBatches);

        for (var batch = 0; batch < totalBatches; batch++)
        {
            var batchFoods = foods.Skip(batch * batchSize).Take(batchSize).ToList();
            var updatedFoodIds = new List<int>();

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
                var existingFood = await _context.FddbFoods.Include(fddbFood => fddbFood.Nutrition)
                    .FirstOrDefaultAsync(f => f.Url == dbFood.Url || f.Ean == dbFood.Ean);
                
                if (existingFood != null)
                {
                    // Keep track of updated foods to update user entries later
                    updatedFoodIds.Add(existingFood.Id);
                    
                    // Update existing food data
                    existingFood.Name = dbFood.Name;
                    existingFood.Description = dbFood.Description;
                    existingFood.ImageUrl = dbFood.ImageUrl;
                    existingFood.Brand = dbFood.Brand;
                    existingFood.Tags = dbFood.Tags;
                    existingFood.Ean = dbFood.Ean;
                    
                    // Update nutrition values including caffeine
                    existingFood.Nutrition.KilojoulesValue = dbFood.Nutrition.KilojoulesValue;
                    existingFood.Nutrition.KilojoulesUnit = dbFood.Nutrition.KilojoulesUnit;
                    existingFood.Nutrition.CaloriesValue = dbFood.Nutrition.CaloriesValue;
                    existingFood.Nutrition.CaloriesUnit = dbFood.Nutrition.CaloriesUnit;
                    existingFood.Nutrition.ProteinValue = dbFood.Nutrition.ProteinValue;
                    existingFood.Nutrition.ProteinUnit = dbFood.Nutrition.ProteinUnit;
                    existingFood.Nutrition.FatValue = dbFood.Nutrition.FatValue;
                    existingFood.Nutrition.FatUnit = dbFood.Nutrition.FatUnit;
                    existingFood.Nutrition.CarbohydratesTotalValue = dbFood.Nutrition.CarbohydratesTotalValue;
                    existingFood.Nutrition.CarbohydratesTotalUnit = dbFood.Nutrition.CarbohydratesTotalUnit;
                    existingFood.Nutrition.CarbohydratesSugarValue = dbFood.Nutrition.CarbohydratesSugarValue;
                    existingFood.Nutrition.CarbohydratesSugarUnit = dbFood.Nutrition.CarbohydratesSugarUnit;
                    existingFood.Nutrition.FiberValue = dbFood.Nutrition.FiberValue;
                    existingFood.Nutrition.FiberUnit = dbFood.Nutrition.FiberUnit;
                    existingFood.Nutrition.CaffeineValue = dbFood.Nutrition.CaffeineValue;
                    existingFood.Nutrition.CaffeineUnit = dbFood.Nutrition.CaffeineUnit;
                    
                    _context.FddbFoods.Update(existingFood);
                }
                else
                {
                    await _context.FddbFoods.AddAsync(dbFood);
                }
            }

            // Save changes for this batch
            await _context.SaveChangesAsync();
            
            // Update user entries for foods that were updated in this batch
            _logger.LogInformation("Updating user entries for {Count} updated foods in batch {Batch}", 
                updatedFoodIds.Count, batch + 1);
                
            foreach (var foodId in updatedFoodIds)
            {
                await _fddbEditorService.UpdateUserEntriesForFoodAsync(foodId);
            }
            
            _logger.LogInformation("Processed batch {Batch}/{TotalBatches}", batch + 1, totalBatches);
        }

        _logger.LogInformation("Import complete for {Count} foods", foods.Count);
    }

    public async Task<int> GetFoodCountAsync()
    {
        return await _context.FddbFoods.CountAsync();
    }
    
    /// <summary>
    /// Removes any ImageUrls that are not valid and fixes names and descriptions by HtmlDecoding them.
    /// </summary>
    public async Task<int> CleanupDataAsync()
    {
        var updatedCount = 0;
        foreach (var food in _context.FddbFoods)
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
            _context.FddbFoods.Update(food);
            updatedCount++;
        }

        await _context.SaveChangesAsync();
        return updatedCount;
    }
}