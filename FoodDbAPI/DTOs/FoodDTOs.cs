using System.ComponentModel.DataAnnotations;
using System.Net;
using FoodDbAPI.DTOs.Base;
using FoodDbAPI.DTOs.Enums;
using FoodDbAPI.Models;
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
    public double Caffeine { get; set; }
    public DateTime ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public static FoodEntryDto MapToFoodEntryDto(FoodEntry entry)
    {
        return new FoodEntryDto
        {
            Id = entry.Id,
            FoodName = WebUtility.HtmlDecode(entry.FoodName),
            FoodUrl = entry.FoodUrl,
            Brand = entry.Brand,
            ImageUrl = entry.ImageUrl,
            GramsConsumed = entry.GramsConsumed,
            Calories = entry.Calories,
            Protein = entry.Protein,
            Fat = entry.Fat,
            Carbohydrates = entry.Carbohydrates,
            Fiber = entry.Fiber,
            Sugar = entry.Sugar,
            Caffeine = entry.Caffeine,
            ConsumedAt = entry.ConsumedAt,
            CreatedAt = entry.CreatedAt
        };
    }
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
    public string? Ean { get; set; }
    public List<string> Tags { get; set; } = new();
    public NutritionInfo Nutrition { get; set; } = new();

    public static FoodSearchDto? MapSavedFoodToDto(FddbFood? food)
    {
        if (food == null)
            return null;
        return new FoodSearchDto
        {
            Id = food.Id,
            Name = WebUtility.HtmlDecode(food.Name),
            Url = food.Url,
            Description = WebUtility.HtmlDecode(food.Description),
            ImageUrl = food.ImageUrl,
            Brand = food.Brand,
            Ean = food.Ean,
            Tags = food.Tags,
            Nutrition = food.Nutrition.ToNutritionInfo()
        };
    }

    public static List<FoodSearchDto> ApplySortingToScrapedFoods(
        List<FoodSearchDto> foods,
        FoodSortBy sortBy,
        SortDirection sortDirection)
    {
        return sortBy switch
        {
            FoodSortBy.Name => sortDirection == SortDirection.Ascending
                ? foods.OrderBy(f => f.Name).ToList()
                : foods.OrderByDescending(f => f.Name).ToList(),
            FoodSortBy.Brand => sortDirection == SortDirection.Ascending
                ? foods.OrderBy(f => f.Brand).ToList()
                : foods.OrderByDescending(f => f.Brand).ToList(),
            FoodSortBy.Calories => sortDirection == SortDirection.Ascending
                ? foods.OrderBy(f => f.Nutrition.Calories.Value).ToList()
                : foods.OrderByDescending(f => f.Nutrition.Calories.Value).ToList(),
            FoodSortBy.Protein => sortDirection == SortDirection.Ascending
                ? foods.OrderBy(f => f.Nutrition.Protein.Value).ToList()
                : foods.OrderByDescending(f => f.Nutrition.Protein.Value).ToList(),
            FoodSortBy.Carbs => sortDirection == SortDirection.Ascending
                ? foods.OrderBy(f => f.Nutrition.Carbohydrates.Total.Value).ToList()
                : foods.OrderByDescending(f => f.Nutrition.Carbohydrates.Total.Value).ToList(),
            FoodSortBy.Fat => sortDirection == SortDirection.Ascending
                ? foods.OrderBy(f => f.Nutrition.Fat.Value).ToList()
                : foods.OrderByDescending(f => f.Nutrition.Fat.Value).ToList(),
            _ => foods.OrderBy(f => f.Name).ToList()
        };
    }
}

public class FoodEntryResponseDto
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

public class FoodSearchResponse : PaginatedResponse
{
    public List<FoodSearchDto> Foods { get; set; } = new();
    public int TotalCount { get; set; }
}

