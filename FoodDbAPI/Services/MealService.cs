using System.Security.Cryptography;
using System.Text;
using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Models;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class MealService(FoodDbContext context, IFoodService foodService, IConfiguration configuration)
    : IMealService
{
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

        return CreateMealResponseDto(meal);
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

        return meals.Select(CreateMealResponseDto).ToList();
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
        var totalMealWeight = meal.MealItems.Sum(mi => mi.Weight);

        if (totalMealWeight <= 0)
        {
            throw new InvalidOperationException("Meal has no items or total weight is zero");
        }

        // Use current time if ConsumedAt is not provided
        var consumedAt = addMealPortionDto.ConsumedAt ?? DateTime.UtcNow;
        var foodEntries = new List<FoodEntryDto>();

        // Create food entries for each meal item based on the portion size
        foreach (var mealItem in meal.MealItems)
        {
            // Calculate the proportion of this item in the meal
            var proportion = mealItem.Weight / totalMealWeight;

            // Calculate how much of this item should be in the consumed portion
            var gramsConsumed = proportion * addMealPortionDto.Weight;

            // Create food entry via the food service
            var createFoodEntryRequest = new CreateFoodEntryRequest
            {
                FddbFoodId = mealItem.FddbFoodId,
                GramsConsumed = gramsConsumed,
                ConsumedAt = consumedAt
            };

            var foodEntry = await foodService.AddFoodEntryAsync(userId, createFoodEntryRequest);
            foodEntries.Add(foodEntry);
        }

        return foodEntries;
    }

    // Helper method to create MealResponseDTO from Meal model
    private MealResponseDto CreateMealResponseDto(Meal meal)
    {
        // Calculate total weight and nutrition values
        var totalWeight = meal.MealItems.Sum(mi => mi.Weight);

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
        var nutrition = new MealNutritionDto();
        if (totalWeight > 0)
        {
            nutrition.Calories = (totalCalories / totalWeight) * 100;
            nutrition.Protein = (totalProtein / totalWeight) * 100;
            nutrition.Fat = (totalFat / totalWeight) * 100;
            nutrition.Carbohydrates = (totalCarbohydrates / totalWeight) * 100;
            nutrition.Fiber = (totalFiber / totalWeight) * 100;
            nutrition.Sugar = (totalSugar / totalWeight) * 100;
        }

        // Create meal items response
        var mealItems = meal.MealItems.Select(mi => new MealItemResponseDto
        {
            Id = mi.Id,
            FddbFoodId = mi.FddbFoodId,
            FoodName = mi.FddbFood?.Name ?? "Unknown Food",
            Weight = mi.Weight,
            Percentage = totalWeight > 0 ? (mi.Weight / totalWeight) * 100 : 0
        }).ToList();

        // Create and return the meal response
        return new MealResponseDto
        {
            Id = meal.Id,
            Name = meal.Name,
            Description = meal.Description ?? string.Empty,
            Items = mealItems,
            TotalWeight = totalWeight,
            Nutrition = nutrition,
            CreatedAt = meal.CreatedAt,
            UpdatedAt = meal.UpdatedAt
        };
    }
}