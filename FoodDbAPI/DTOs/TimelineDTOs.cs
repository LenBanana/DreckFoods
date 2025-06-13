using FoodDbAPI.DTOs.Base;

namespace FoodDbAPI.DTOs;

public class DailyTimelineDto : NutritionBase
{
    public DateTime Date { get; set; }
    // Nutritional properties are inherited from NutritionBase
    // TotalCalories -> Calories
    // TotalProtein -> Protein
    // TotalFat -> Fat
    // TotalCarbohydrates -> Carbohydrates
    // TotalSugar -> Sugar
    // TotalFiber -> Fiber
    // TotalCaffeine -> Caffeine
    public List<FoodEntryDto> FoodEntries { get; set; } = new();
    public WeightEntryDto? WeightEntry { get; set; }
}

public class TimelineResponse
{
    public List<DailyTimelineDto> Days { get; set; } = new();
    public int TotalDays { get; set; }
}