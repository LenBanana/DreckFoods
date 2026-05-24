using System.ComponentModel.DataAnnotations;
using FoodDbAPI.Models.Fddb;

namespace FoodDbAPI.DTOs;

public class StartSessionRequestDto
{
    /// <summary>
    /// "log"  — user describes what they ate; agent logs it (default).
    /// "plan" — user wants to build a recipe/meal composition to save as a template.
    /// </summary>
    public string Mode { get; set; } = "log";
}

public class StartSessionResponseDto
{
    public string SessionId { get; set; } = string.Empty;
}

public class SendMessageRequestDto
{
    [Required]
    public string Message { get; set; } = string.Empty;
}

public class MealDraftItemDto
{
    public int FddbFoodId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public double WeightGrams { get; set; }
    public string? ImageUrl { get; set; }
    public NutritionInfo? Nutrition { get; set; }  // per 100g
}

public class MealDraftDto
{
    public List<MealDraftItemDto> Items { get; set; } = new();
}

public class ConfirmMealRequestDto
{
    [Required]
    [MinLength(1)]
    public string Name { get; set; } = string.Empty;

    public bool SaveAsMeal { get; set; } = true;

    /// <summary>
    /// When false, saves only as a meal template without logging any food entries.
    /// Implies SaveAsMeal = true; ConsumedAt is ignored.
    /// Defaults to true for backwards compatibility.
    /// </summary>
    public bool LogNow { get; set; } = true;

    /// <summary>UTC timestamp; if null the backend uses DateTime.UtcNow.</summary>
    public DateTime? ConsumedAt { get; set; }
}

public class ConfirmMealResponseDto
{
    /// <summary>The created reusable meal template. Null when SaveAsMeal is false.</summary>
    public MealResponseDto? Meal { get; set; }

    public List<FoodEntryDto> Entries { get; set; } = new();
}
