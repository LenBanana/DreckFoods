namespace FoodDbAPI.Models.Fddb;

public class NutritionInfo
{
    public NutritionalValue Kilojoules { get; set; } = new();
    public NutritionalValue Calories { get; set; } = new();
    public NutritionalValue Protein { get; set; } = new();
    public NutritionalValue Fat { get; set; } = new();
    public CarbohydrateInfo Carbohydrates { get; set; } = new();
    public MineralInfo Minerals { get; set; } = new();
    public NutritionalValue Fiber { get; set; } = new();
    public NutritionalValue Caffeine { get; set; } = new();
}