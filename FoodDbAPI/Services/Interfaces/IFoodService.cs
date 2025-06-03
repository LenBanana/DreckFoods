using FoodDbAPI.DTOs;

namespace FoodDbAPI.Services.Interfaces;

public interface IFoodService
{
    Task<FoodSearchResponse> SearchFoodsAsync(string query, int page = 1, int pageSize = 20);
    Task<FoodSearchDto?> GetFoodByIdAsync(int foodId);
    Task<FoodEntryDto> AddFoodEntryAsync(int userId, CreateFoodEntryRequest request);
    Task<List<FoodEntryDto>> GetFoodEntriesAsync(int userId, DateTime? date = null);
    Task DeleteFoodEntryAsync(int userId, int entryId);
    Task<List<DailyTimelineDto>> GetTimelineAsync(int userId, DateTime startDate, DateTime endDate);
}