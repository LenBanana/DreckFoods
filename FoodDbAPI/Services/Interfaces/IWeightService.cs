using FoodDbAPI.DTOs;

namespace FoodDbAPI.Services.Interfaces;

public interface IWeightService
{
    Task<WeightEntryDto> AddWeightEntryAsync(int userId, CreateWeightEntryRequest request);
    Task<List<WeightEntryDto>> GetWeightHistoryAsync(int userId, DateTime? startDate = null, DateTime? endDate = null);
    Task DeleteWeightEntryAsync(int userId, int entryId);
}