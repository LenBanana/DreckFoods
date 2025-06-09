using FoodDbAPI.DTOs;

namespace FoodDbAPI.Services.Interfaces;

public interface ITimelineService
{
    Task<List<DailyTimelineDto>> GetTimelineAsync(int userId, DateTime startDate, DateTime endDate);
}
