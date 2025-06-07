using FoodDbAPI.DTOs;

namespace FoodDbAPI.Services.Interfaces;

public interface IFddbScrapingService
{
    Task<List<FddbFoodImportDTO>> FindFoodItemByNameAsync(string foodName, CancellationToken cancellationToken = default);
}