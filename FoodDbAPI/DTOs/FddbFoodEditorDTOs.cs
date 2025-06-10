using FoodDbAPI.Models.Fddb;

namespace FoodDbAPI.DTOs;

public class FddbFoodUpdateDTO
{
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? Brand { get; set; }
    public string? Ean { get; set; }
    public List<string>? Tags { get; set; }
}

public class FddbFoodNutritionUpdateDTO
{
    // Kilojoules
    public double? KilojoulesValue { get; set; }
    public string? KilojoulesUnit { get; set; }

    // Calories
    public double? CaloriesValue { get; set; }
    public string? CaloriesUnit { get; set; }

    // Protein
    public double? ProteinValue { get; set; }
    public string? ProteinUnit { get; set; }

    // Fat
    public double? FatValue { get; set; }
    public string? FatUnit { get; set; }

    // Carbohydrates
    public double? CarbohydratesTotalValue { get; set; }
    public string? CarbohydratesTotalUnit { get; set; }
    public double? CarbohydratesSugarValue { get; set; }
    public string? CarbohydratesSugarUnit { get; set; }
    public double? CarbohydratesPolyolsValue { get; set; }
    public string? CarbohydratesPolyolsUnit { get; set; }

    // Fiber
    public double? FiberValue { get; set; }
    public string? FiberUnit { get; set; }

    // Minerals
    public double? SaltValue { get; set; }
    public string? SaltUnit { get; set; }
    public double? IronValue { get; set; }
    public string? IronUnit { get; set; }
    public double? ZincValue { get; set; }
    public string? ZincUnit { get; set; }
    public double? MagnesiumValue { get; set; }
    public string? MagnesiumUnit { get; set; }
    public double? ChlorideValue { get; set; }
    public string? ChlorideUnit { get; set; }
    public double? ManganeseValue { get; set; }
    public string? ManganeseUnit { get; set; }
    public double? SulfurValue { get; set; }
    public string? SulfurUnit { get; set; }
    public double? PotassiumValue { get; set; }
    public string? PotassiumUnit { get; set; }
    public double? CalciumValue { get; set; }
    public string? CalciumUnit { get; set; }
    public double? PhosphorusValue { get; set; }
    public string? PhosphorusUnit { get; set; }
    public double? CopperValue { get; set; }
    public string? CopperUnit { get; set; }
    public double? FluorideValue { get; set; }
    public string? FluorideUnit { get; set; }
    public double? IodineValue { get; set; }
    public string? IodineUnit { get; set; }
    public double? CaffeineValue { get; set; }
    public string? CaffeineUnit { get; set; }
}

public class FddbFoodCompleteUpdateDTO
{
    public FddbFoodUpdateDTO? FoodInfo { get; set; }
    public FddbFoodNutritionUpdateDTO? Nutrition { get; set; }
}
