using FoodDbAPI.Models.Fddb;

namespace FoodDbAPI.Services.AI.Agent;

/// <summary>
/// Represents a single food item in the meal draft being assembled by the AI agent.
/// <see cref="Nutrition"/> is per 100 g — the frontend scales by <see cref="WeightGrams"/> / 100.
/// </summary>
public class MealDraftItem
{
    public int FddbFoodId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public double WeightGrams { get; set; }
    public string? ImageUrl { get; set; }
    public NutritionInfo? Nutrition { get; set; }
}
