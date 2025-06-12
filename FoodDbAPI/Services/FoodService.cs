using FoodDbAPI.DTOs;
using FoodDbAPI.DTOs.Enums;
using FoodDbAPI.Services.Interfaces;

namespace FoodDbAPI.Services;

public class FoodService(
    IFoodSearchService foodSearchService,
    IFoodEntryService foodEntryService,
    ITimelineService timelineService)
    : IFoodService
{
    // Delegating to FoodSearchService
    public Task<FoodSearchResponse> SearchFoodsAsync(
        string query, 
        int? userId = null,
        int page = 1, 
        int pageSize = 20,
        FoodSortBy sortBy = FoodSortBy.Name,
        SortDirection sortDirection = SortDirection.Ascending)
    {
        return foodSearchService.SearchFoodsAsync(query, userId, page, pageSize, sortBy, sortDirection);
    }

    public Task<List<string>> GetFoodCategoriesAsync()
    {
        return foodSearchService.GetFoodCategoriesAsync();
    }

    public Task<FoodSearchDto?> GetFoodByIdAsync(int foodId)
    {
        return foodSearchService.GetFoodByIdAsync(foodId);
    }

    public Task<FoodSearchResponse> GetPastEatenFoodsAsync(int userId, int page = 1, int pageSize = 20)
    {
        return foodSearchService.GetPastEatenFoodsAsync(userId, page, pageSize);
    }

    // Delegating to FoodEntryService
    public Task<FoodEntryDto> AddFoodEntryAsync(int userId, CreateFoodEntryRequest request)
    {
        return foodEntryService.AddFoodEntryAsync(userId, request);
    }

    public Task<FoodEntryDto> EditFoodEntryAsync(int userId, EditFoodEntryRequest request)
    {
        return foodEntryService.EditFoodEntryAsync(userId, request);
    }

    public Task<List<FoodEntryDto>> GetFoodEntriesAsync(int userId, DateTime? date = null)
    {
        return foodEntryService.GetFoodEntriesAsync(userId, date);
    }

    public Task DeleteFoodEntryAsync(int userId, int entryId)
    {
        return foodEntryService.DeleteFoodEntryAsync(userId, entryId);
    }

    // Delegating to TimelineService
    public Task<List<DailyTimelineDto>> GetTimelineAsync(int userId, DateTime startDate, DateTime endDate)
    {
        return timelineService.GetTimelineAsync(userId, startDate, endDate);
    }
}