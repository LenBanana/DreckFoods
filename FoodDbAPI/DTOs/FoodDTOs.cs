using System.ComponentModel.DataAnnotations;
using FoodDbAPI.Models.Fddb;

namespace FoodDbAPI.DTOs;

public class FoodEntryDto
{
    public int Id { get; set; }
    public string FoodName { get; set; } = string.Empty;
    public string? FoodUrl { get; set; }
    public string? Brand { get; set; }
    public string? ImageUrl { get; set; }
    public double GramsConsumed { get; set; }
    public double Calories { get; set; }
    public double Protein { get; set; }
    public double Fat { get; set; }
    public double Carbohydrates { get; set; }
    public double Fiber { get; set; }
    public double Sugar { get; set; }
    public DateTime ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateFoodEntryRequest
{
    [Required] public int FddbFoodId { get; set; }

    [Required] [Range(0.1, 10000)] public double GramsConsumed { get; set; }

    [Required] public DateTime ConsumedAt { get; set; }
}

public class EditFoodEntryRequest
{
    [Required] public int FddbFoodId { get; set; }

    [Required] [Range(0.1, 10000)] public double GramsConsumed { get; set; }
}

public class FoodSearchDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public NutritionInfo Nutrition { get; set; } = new();
}

public class FoodSearchResponse
{
    public List<FoodSearchDto> Foods { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}