using System.Security.Cryptography;
using System.Text;
using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Models;
using FoodDbAPI.Models.Fddb;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class MealService(FoodDbContext context, IFoodService foodService, IConfiguration configuration)
    : IMealService
{
    private const int MaxServingSuggestions = 6;

    public async Task<MealResponseDto> CreateMealAsync(int userId, CreateMealDto createMealDto)
    {
        // Verify all food items exist
        var foodIds = createMealDto.Items.Select(i => i.FddbFoodId).ToList();
        var foods = await context.FddbFoods
            .Include(f => f.Nutrition)
            .Where(f => foodIds.Contains(f.Id))
            .ToListAsync();

        if (foods.Count != foodIds.Count)
        {
            throw new ArgumentException("One or more food items do not exist");
        }

        var meal = new Meal
        {
            Name = createMealDto.Name,
            Description = createMealDto.Description,
            CookedWeight = createMealDto.CookedWeight,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Meals.Add(meal);
        await context.SaveChangesAsync();

        // Add meal items
        foreach (var mealItem in createMealDto.Items.Select(item => new MealItem
                 {
                     MealId = meal.Id,
                     FddbFoodId = item.FddbFoodId,
                     Weight = item.Weight
                 }))
        {
            context.MealItems.Add(mealItem);
        }

        await context.SaveChangesAsync();

        // Load meal with items for the response
        return await GetMealByIdAsync(meal.Id, userId);
    }

    public string GetMealShareId(int mealId, int userId)
    {
        var shareSecret = configuration.GetValue<string>("ShareSettings:Secret");
        if (string.IsNullOrEmpty(shareSecret))
            throw new InvalidOperationException("Share secret is not configured");

        var payload = $"{mealId}:{userId}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(shareSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var signature = Convert.ToBase64String(hash);

        var token = $"{payload}:{signature}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
    }

    public async Task<MealResponseDto> AddMealByShareIdAsync(string shareId, int userId)
    {
        try
        {
            var shareSecret = configuration.GetValue<string>("ShareSettings:Secret");
            if (string.IsNullOrEmpty(shareSecret))
                throw new InvalidOperationException("Share secret is not configured");

            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(shareId));
            var parts = decoded.Split(':');

            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out var mealId) ||
                !int.TryParse(parts[1], out var sharedUserId))
                throw new ArgumentException("Invalid share ID format");

            var payload = $"{parts[0]}:{parts[1]}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(shareSecret));
            var expectedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expectedSignature = Convert.ToBase64String(expectedHash);

            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(expectedSignature),
                    Convert.FromBase64String(parts[2])))
            {
                throw new ArgumentException("Invalid share ID signature");
            }

            // Check if the meal belongs to the shared user
            var sharedMeal = await context.Meals
                .Include(m => m.MealItems) // Ensure meal items are loaded
                .FirstOrDefaultAsync(m => m.Id == mealId && m.UserId == sharedUserId);

            if (sharedMeal == null)
            {
                throw new KeyNotFoundException("Shared meal not found");
            }

            // Check if the current user already has a meal with this name (potential duplicate)
            var existingMeal = await context.Meals
                .FirstOrDefaultAsync(m => m.UserId == userId && m.Name == sharedMeal.Name);

            if (existingMeal != null)
            {
                throw new InvalidOperationException(
                    "You already have a meal with this name. Please delete it first or use a different share code.");
            }

            // Create a new meal for the current user
            var newMeal = new Meal
            {
                Name = sharedMeal.Name,
                Description = sharedMeal.Description,
                CookedWeight = sharedMeal.CookedWeight,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Meals.Add(newMeal);
            await context.SaveChangesAsync();

            // Copy meal items to the new meal
            if (sharedMeal.MealItems.Count == 0)
                return await GetMealByIdAsync(newMeal.Id, userId);

            foreach (var item in sharedMeal.MealItems)
            {
                var newMealItem = new MealItem
                {
                    MealId = newMeal.Id,
                    FddbFoodId = item.FddbFoodId,
                    Weight = item.Weight
                };

                context.MealItems.Add(newMealItem);
            }

            await context.SaveChangesAsync();

            // Return the newly created meal
            return await GetMealByIdAsync(newMeal.Id, userId);
        }
        catch (FormatException)
        {
            throw new ArgumentException("Invalid share ID format");
        }
    }

    public async Task<MealResponseDto> GetMealByIdAsync(int mealId, int userId)
    {
        var meal = await context.Meals
            .Include(m => m.MealItems)
            .ThenInclude(mi => mi.FddbFood)
            .ThenInclude(f => f.Nutrition)
            .FirstOrDefaultAsync(m => m.Id == mealId && m.UserId == userId);

        if (meal == null)
        {
            throw new KeyNotFoundException("Meal not found");
        }

        var servingHistory = await LoadMealServingHistoryAsync(userId, [meal.Id]);
        return CreateMealResponseDto(meal, servingHistory);
    }
    
    public async Task<MealResponseDto> DuplicateMealAsync(int mealId, int userId)
    {
        var originalMeal = await context.Meals
            .Include(m => m.MealItems)
            .ThenInclude(mi => mi.FddbFood)
            .ThenInclude(f => f.Nutrition)
            .FirstOrDefaultAsync(m => m.Id == mealId && m.UserId == userId);

        if (originalMeal == null)
        {
            throw new KeyNotFoundException("Original meal not found");
        }

        // Create a new meal with the same properties
        var duplicatedMeal = new Meal
        {
            Name = originalMeal.Name + " (Copy)",
            Description = originalMeal.Description,
            CookedWeight = originalMeal.CookedWeight,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Meals.Add(duplicatedMeal);
        await context.SaveChangesAsync();

        // Copy meal items to the new meal
        foreach (var item in originalMeal.MealItems)
        {
            var newMealItem = new MealItem
            {
                MealId = duplicatedMeal.Id,
                FddbFoodId = item.FddbFoodId,
                Weight = item.Weight
            };

            context.MealItems.Add(newMealItem);
        }

        await context.SaveChangesAsync();

        // Return the newly created duplicated meal
        return await GetMealByIdAsync(duplicatedMeal.Id, userId);
    }

    public async Task<List<MealResponseDto>> GetUserMealsAsync(int userId)
    {
        var meals = await context.Meals
            .Include(m => m.MealItems)
            .ThenInclude(mi => mi.FddbFood)
            .ThenInclude(f => f.Nutrition)
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.UpdatedAt)
            .ToListAsync();

        var servingHistory = await LoadMealServingHistoryAsync(userId, meals.Select(m => m.Id));
        return meals.Select(meal => CreateMealResponseDto(meal, servingHistory)).ToList();
    }

    public async Task<MealResponseDto> UpdateMealAsync(int mealId, int userId, UpdateMealDto updateMealDto)
    {
        var meal = await context.Meals
            .Include(m => m.MealItems)
            .FirstOrDefaultAsync(m => m.Id == mealId && m.UserId == userId);

        if (meal == null)
        {
            throw new KeyNotFoundException("Meal not found");
        }

        // Update basic properties if provided
        if (updateMealDto.Name != null)
        {
            meal.Name = updateMealDto.Name;
        }

        if (updateMealDto.Description != null)
        {
            meal.Description = updateMealDto.Description;
        }

        // Apply cooked weight when the client explicitly flags it (allows clearing to null)
        if (updateMealDto.UpdateCookedWeight)
        {
            meal.CookedWeight = updateMealDto.CookedWeight;
        }

        // Update items if provided
        if (updateMealDto.Items != null)
        {
            // Verify all food items exist
            var foodIds = updateMealDto.Items.Select(i => i.FddbFoodId).ToList();
            var foods = await context.FddbFoods
                .Where(f => foodIds.Contains(f.Id))
                .ToListAsync();

            if (foods.Count != foodIds.Count)
            {
                throw new ArgumentException("One or more food items do not exist");
            }

            // Remove existing meal items
            context.MealItems.RemoveRange(meal.MealItems);

            // Add new meal items
            foreach (var item in updateMealDto.Items)
            {
                var mealItem = new MealItem
                {
                    MealId = meal.Id,
                    FddbFoodId = item.FddbFoodId,
                    Weight = item.Weight
                };

                context.MealItems.Add(mealItem);
            }
        }

        meal.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        // Return updated meal
        return await GetMealByIdAsync(meal.Id, userId);
    }

    public async Task<bool> DeleteMealAsync(int mealId, int userId)
    {
        var meal = await context.Meals
            .FirstOrDefaultAsync(m => m.Id == mealId && m.UserId == userId);

        if (meal == null)
        {
            return false;
        }

        context.Meals.Remove(meal);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<List<FoodEntryDto>> AddMealPortionAsync(int userId, AddMealPortionDto addMealPortionDto)
    {
        var meal = await context.Meals
            .Include(m => m.MealItems)
            .ThenInclude(mi => mi.FddbFood)
            .ThenInclude(f => f.Nutrition)
            .FirstOrDefaultAsync(m => m.Id == addMealPortionDto.MealId && m.UserId == userId);

        if (meal == null)
        {
            throw new KeyNotFoundException("Meal not found");
        }

        // Calculate total meal weight
        var totalRawWeight = meal.MealItems.Sum(mi => mi.Weight);

        if (totalRawWeight <= 0)
        {
            throw new InvalidOperationException("Meal has no items or total weight is zero");
        }

        // When CookedWeight is set, the user is logging grams of the *cooked* meal.
        // Each raw ingredient must be scaled by (portionWeight / cookedWeight) rather than
        // (portionWeight / totalRawWeight), because the cooking process concentrates macros.
        // Example: 450 g raw → 300 g cooked. Logging 200 g cooked means the user consumed
        //   (200/300) of each raw ingredient, not (200/450).
        var effectiveWeight = meal.CookedWeight ?? totalRawWeight;

        if (effectiveWeight <= 0)
        {
            throw new InvalidOperationException("Effective meal weight is zero");
        }

        // Use current time if ConsumedAt is not provided
        var consumedAt = addMealPortionDto.ConsumedAt ?? DateTime.UtcNow;
        var foodEntries = new List<FoodEntryDto>();

        await using var transaction = await context.Database.BeginTransactionAsync();

        // Create food entries for each meal item based on the portion size
        foreach (var mealItem in meal.MealItems)
        {
            // gramsConsumed = (rawItemWeight / effectiveWeight) × portionWeight
            // Dividing by effectiveWeight (cooked) instead of totalRawWeight correctly
            // accounts for the water/mass lost during cooking.
            var gramsConsumed = (mealItem.Weight / effectiveWeight) * addMealPortionDto.Weight;

            // Create food entry via the food service
            var createFoodEntryRequest = new CreateFoodEntryRequest
            {
                FddbFoodId = mealItem.FddbFoodId,
                GramsConsumed = gramsConsumed,
                ServingName = addMealPortionDto.ServingName,
                ConsumedAt = consumedAt
            };

            var foodEntry = await foodService.AddFoodEntryAsync(userId, createFoodEntryRequest);
            foodEntries.Add(foodEntry);
        }

        context.MealPortionLogs.Add(new MealPortionLog
        {
            UserId = userId,
            MealId = meal.Id,
            Weight = addMealPortionDto.Weight,
            ServingName = NormalizeServingName(addMealPortionDto.ServingName),
            ConsumedAt = consumedAt,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return foodEntries;
    }

    // Helper method to create MealResponseDTO from Meal model
    private MealResponseDto CreateMealResponseDto(
        Meal meal,
        IReadOnlyDictionary<int, List<ServingInfo>>? servingHistory = null)
    {
        // Calculate total weight and nutrition values
        var totalRawWeight = meal.MealItems.Sum(mi => mi.Weight);
        // Effective weight drives per-100g concentration and portion math.
        // When CookedWeight is set it reflects real post-cooking mass (e.g. a stew);
        // otherwise fall back to the sum of raw ingredient weights.
        var effectiveWeight = meal.CookedWeight ?? totalRawWeight;

        // Initialize nutrition totals
        double totalCalories = 0;
        double totalProtein = 0;
        double totalFat = 0;
        double totalCarbohydrates = 0;
        double totalFiber = 0;
        double totalSugar = 0;

        // Calculate total nutrition values
        foreach (var item in meal.MealItems)
        {
            if (item.FddbFood?.Nutrition == null) continue;
            // Calculate the actual nutrition values for this item based on its weight
            var weight = item.Weight;
            var caloriesPer100G = item.FddbFood.Nutrition.CaloriesValue;
            var proteinPer100G = item.FddbFood.Nutrition.ProteinValue;
            var fatPer100G = item.FddbFood.Nutrition.FatValue;
            var carbsPer100G = item.FddbFood.Nutrition.CarbohydratesTotalValue;
            var fiberPer100G = item.FddbFood.Nutrition.FiberValue;
            var sugarPer100G = item.FddbFood.Nutrition.CarbohydratesSugarValue;

            // Add to totals
            totalCalories += (caloriesPer100G * weight / 100);
            totalProtein += (proteinPer100G * weight / 100);
            totalFat += (fatPer100G * weight / 100);
            totalCarbohydrates += (carbsPer100G * weight / 100);
            totalFiber += (fiberPer100G * weight / 100);
            totalSugar += (sugarPer100G * weight / 100);
        }

        // Calculate nutrition values per 100g for the entire meal
        // Using effectiveWeight so that cooked meals report correct macro density.
        var nutrition = new MealNutritionDto();
        if (effectiveWeight > 0)
        {
            nutrition.Calories = (totalCalories / effectiveWeight) * 100;
            nutrition.Protein = (totalProtein / effectiveWeight) * 100;
            nutrition.Fat = (totalFat / effectiveWeight) * 100;
            nutrition.Carbohydrates = (totalCarbohydrates / effectiveWeight) * 100;
            nutrition.Fiber = (totalFiber / effectiveWeight) * 100;
            nutrition.Sugar = (totalSugar / effectiveWeight) * 100;
        }

        // Create meal items response — percentages are of raw total (ingredient composition)
        var mealItems = meal.MealItems.Select(mi => new MealItemResponseDto
        {
            Id = mi.Id,
            FddbFoodId = mi.FddbFoodId,
            FoodName = mi.FddbFood?.Name ?? "Unknown Food",
            Weight = mi.Weight,
            Percentage = totalRawWeight > 0 ? (mi.Weight / totalRawWeight) * 100 : 0
        }).ToList();

        // Create and return the meal response
        return new MealResponseDto
        {
            Id = meal.Id,
            Name = meal.Name,
            Description = meal.Description ?? string.Empty,
            Items = mealItems,
            Servings = BuildMealServingSuggestions(meal, servingHistory),
            TotalWeight = effectiveWeight,
            RawTotalWeight = totalRawWeight,
            CookedWeight = meal.CookedWeight,
            Nutrition = nutrition,
            CreatedAt = meal.CreatedAt,
            UpdatedAt = meal.UpdatedAt
        };
    }

    private async Task<Dictionary<int, List<ServingInfo>>> LoadMealServingHistoryAsync(
        int userId,
        IEnumerable<int> mealIds)
    {
        var distinctMealIds = mealIds.Distinct().ToList();
        if (distinctMealIds.Count == 0)
            return [];

        var rows = await context.MealPortionLogs
            .AsNoTracking()
            .Where(log => log.UserId == userId && distinctMealIds.Contains(log.MealId))
            .OrderByDescending(log => log.ConsumedAt)
            .Select(log => new
            {
                log.MealId,
                log.Weight,
                log.ServingName
            })
            .ToListAsync();

        return rows
            .GroupBy(row => row.MealId)
            .ToDictionary(
                group => group.Key,
                group => BuildServingHistory(group.Select(row => new ServingInfo
                {
                    Name = string.IsNullOrWhiteSpace(row.ServingName)
                        ? FormatWeightServingName(row.Weight)
                        : row.ServingName.Trim(),
                    WeightGrams = row.Weight
                })));
    }

    private static List<ServingInfo> BuildMealServingSuggestions(
        Meal meal,
        IReadOnlyDictionary<int, List<ServingInfo>>? servingHistory)
    {
        if (servingHistory != null && servingHistory.TryGetValue(meal.Id, out var suggestions))
            return suggestions;

        return [];
    }

    private static List<ServingInfo> BuildServingHistory(IEnumerable<ServingInfo> suggestions)
    {
        var merged = new List<ServingInfo>();

        foreach (var suggestion in suggestions)
        {
            if (suggestion.WeightGrams <= 0)
                continue;

            if (merged.Any(existing => Math.Abs(existing.WeightGrams - suggestion.WeightGrams) < 0.5))
                continue;

            merged.Add(suggestion);
            if (merged.Count == MaxServingSuggestions)
                break;
        }

        return merged;
    }

    private static string FormatWeightServingName(double grams) => $"{grams:0.#} g";

    private static string? NormalizeServingName(string? servingName)
    {
        if (string.IsNullOrWhiteSpace(servingName))
            return null;

        return servingName.Trim();
    }
}