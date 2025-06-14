using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class TimelineService(
    FoodDbContext context,
    ILogger<TimelineService> logger) : ITimelineService
{
    private readonly ILogger<TimelineService> _logger = logger;

    public async Task<List<DailyTimelineDto>> GetTimelineAsync(int userId, DateTime startDate, DateTime endDate)
    {
        var foodEntries = await context.FoodEntries
            .Where(f => f.UserId == userId &&
                        f.ConsumedAt >= startDate &&
                        f.ConsumedAt <= endDate)
            .OrderBy(f => f.ConsumedAt)
            .ToListAsync();

        var weightEntries = await context.WeightEntries
            .Where(w => w.UserId == userId &&
                        w.RecordedAt >= startDate &&
                        w.RecordedAt <= endDate)
            .ToListAsync();

        var timeline = new List<DailyTimelineDto>();
        var currentDate = endDate.Date;

        while (currentDate >= startDate.Date)
        {
            var dayFoodEntries = foodEntries
                .Where(f => f.ConsumedAt.Date == currentDate)
                .ToList();

            var dayWeightEntry = weightEntries
                .FirstOrDefault(w => w.RecordedAt.Date == currentDate);

            var dailyData = new DailyTimelineDto
            {
                Date = currentDate,
                Calories = dayFoodEntries.Sum(f => f.Calories),
                Protein = dayFoodEntries.Sum(f => f.Protein),
                Fat = dayFoodEntries.Sum(f => f.Fat),
                Carbohydrates = dayFoodEntries.Sum(f => f.Carbohydrates),
                Sugar = dayFoodEntries.Sum(f => f.Sugar),
                Fiber = dayFoodEntries.Sum(f => f.Fiber),
                Caffeine = dayFoodEntries.Sum(f => f.Caffeine),
                Salt = dayFoodEntries.Sum(f => f.Salt),
                FoodEntries = dayFoodEntries.Select(FoodEntryDto.MapToFoodEntryDto).ToList(),
                WeightEntry = dayWeightEntry != null
                    ? new WeightEntryDto
                    {
                        Id = dayWeightEntry.Id,
                        Weight = dayWeightEntry.Weight,
                        RecordedAt = dayWeightEntry.RecordedAt,
                        CreatedAt = dayWeightEntry.CreatedAt
                    }
                    : null
            };

            timeline.Add(dailyData);
            currentDate = currentDate.AddDays(-1);
        }

        return timeline;
    }
}
