using FoodDbAPI.Models;
using FoodDbAPI.Models.Fddb;

namespace FoodDbAPI.DTOs;

public class CreateMealDto
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required List<MealItemDto> Items { get; set; }
}

public class MealItemDto
{
    public required int FddbFoodId { get; set; }
    public required double Weight { get; set; } // In grams
}

public class UpdateMealDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<MealItemDto>? Items { get; set; }
}

public class MealResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<MealItemResponseDto> Items { get; set; } = new();
    public double TotalWeight { get; set; } // In grams
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
    public DateTime? ConsumedAt { get; set; } // If not provided, current time will be used
}
