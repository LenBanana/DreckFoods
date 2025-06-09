using FoodDbAPI.DTOs;

namespace FoodDbAPI.Services.Interfaces;

public interface IDataImportService
{
    Task ImportFoodDataAsync(List<FddbFoodImportDto> foods);
    Task<int> GetFoodCountAsync();
    Task<int> CleanupDataAsync();
}