using System;

namespace FoodDbAPI.DTOs.Base;

/// <summary>
/// Base class for all nutrition-related DTOs and models
/// This centralizes the nutritional properties to reduce redundancy
/// </summary>
public abstract class NutritionBase
{
    public double Calories { get; set; }
    public double Protein { get; set; }
    public double Fat { get; set; }
    public double Carbohydrates { get; set; }
    public double Fiber { get; set; }
    public double Sugar { get; set; }
    public double Caffeine { get; set; }
    public double Salt { get; set; }
    
    /// <summary>
    /// Apply a multiplier to all nutritional values (e.g., for portion calculations)
    /// </summary>
    public void ApplyMultiplier(double multiplier)
    {
        Calories *= multiplier;
        Protein *= multiplier;
        Fat *= multiplier;
        Carbohydrates *= multiplier;
        Fiber *= multiplier;
        Sugar *= multiplier;
        Caffeine *= multiplier;
        Salt *= multiplier;
    }
    
    /// <summary>
    /// Create a new NutritionBase with values multiplied by the given factor
    /// </summary>
    public T WithMultiplier<T>(double multiplier) where T : NutritionBase, new()
    {
        var result = new T
        {
            Calories = Calories * multiplier,
            Protein = Protein * multiplier,
            Fat = Fat * multiplier,
            Carbohydrates = Carbohydrates * multiplier,
            Fiber = Fiber * multiplier,
            Sugar = Sugar * multiplier,
            Caffeine = Caffeine * multiplier,
            Salt = Salt * multiplier
        };
        
        return result;
    }
}
