using System.ComponentModel.DataAnnotations;
using FoodDbAPI.Models.Fddb;

namespace FoodDbAPI.DTOs;

public class CreateMealDto
{
    [MaxLength(200)]
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required List<MealItemDto> Items { get; set; }
    [Range(1, 100_000, ErrorMessage = "Cooked weight must be between 1 and 100 000 g.")]
    public double? CookedWeight { get; set; }
}

public class MealItemDto
{
    public required int FddbFoodId { get; set; }
    public required double Weight { get; set; } // In grams
}

public class UpdateMealDto
{
    [MaxLength(200)]
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<MealItemDto>? Items { get; set; }
    /// <summary>Always applied when present in the request. Null clears the cooked weight.</summary>
    [Range(1, 100_000, ErrorMessage = "Cooked weight must be between 1 and 100 000 g.")]
    public double? CookedWeight { get; set; }
    public bool UpdateCookedWeight { get; set; } = false;
}

public class MealResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<MealItemResponseDto> Items { get; set; } = new();
    public List<ServingInfo> Servings { get; set; } = new();
    /// <summary>Effective total weight used for macro concentration — equals CookedWeight when set, otherwise sum of raw ingredient weights.</summary>
    public double TotalWeight { get; set; } // In grams
    public double RawTotalWeight { get; set; } // Sum of raw ingredient weights, always present
    public double? CookedWeight { get; set; } // Optional user-supplied post-cooking weight
    public MealNutritionDto Nutrition { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MealItemResponseDto
{
    public int Id { get; set; }
    public int FddbFoodId { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public double Weight { get; set; } // In grams
    public double Percentage { get; set; } // Percentage of total meal weight
}

public class MealNutritionDto
{
    // Per 100g of the meal
    public double Calories { get; set; }
    public double Protein { get; set; }
    public double Fat { get; set; }
    public double Carbohydrates { get; set; }
    public double Fiber { get; set; }
    public double Sugar { get; set; }
}

public class AddMealPortionDto
{
    public required int MealId { get; set; }
    public required double Weight { get; set; } // In grams
    [MaxLength(200)] public string? ServingName { get; set; }
    public DateTime? ConsumedAt { get; set; } // If not provided, current time will be used
}
