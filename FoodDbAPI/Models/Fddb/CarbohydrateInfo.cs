namespace FoodDbAPI.Models.Fddb;

public class CarbohydrateInfo
{
    public NutritionalValue Total { get; set; } = new();
    public NutritionalValue Sugar { get; set; } = new();
    public NutritionalValue Polyols { get; set; } = new();
}