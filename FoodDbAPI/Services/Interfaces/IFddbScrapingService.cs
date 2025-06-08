using FoodDbAPI.DTOs;

namespace FoodDbAPI.Services.Interfaces;

public interface IFddbScrapingService
{
    Task<List<FddbFoodImportDto>> FindFoodItemByNameAsync(string foodName, CancellationToken cancellationToken = default);
}