namespace FoodDbAPI.DTOs;

public class DailyTimelineDto
{
    public DateTime Date { get; set; }
    public double TotalCalories { get; set; }
    public double TotalProtein { get; set; }
    public double TotalFat { get; set; }
    public double TotalCarbohydrates { get; set; }
    public double TotalFiber { get; set; }
    public List<FoodEntryDto> FoodEntries { get; set; } = new();
    public WeightEntryDto? WeightEntry { get; set; }
}

public class TimelineResponse
{
    public List<DailyTimelineDto> Days { get; set; } = new();
    public int TotalDays { get; set; }
}