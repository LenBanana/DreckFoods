using FoodDbAPI.DTOs;

namespace FoodDbAPI.Services.Interfaces;

public interface IFoodEntryService
{
    Task<FoodEntryDto> AddFoodEntryAsync(int userId, CreateFoodEntryRequest request);
    Task<FoodEntryDto> EditFoodEntryAsync(int userId, EditFoodEntryRequest request);
    Task<List<FoodEntryDto>> GetFoodEntriesAsync(int userId, DateTime? date = null);
    Task DeleteFoodEntryAsync(int userId, int entryId);
}
