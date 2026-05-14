namespace FoodDbAPI.Models.Fddb;

public class ServingInfo
{
    public string Name { get; set; }
    public string Unit { get; set; }
    public double Amount { get; set; }
    public NutritionalValue Kilojoules { get; set; }
    public NutritionalValue Calories { get; set; }
}