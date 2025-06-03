using FoodDbAPI.Models.Fddb;

namespace FoodDbAPI.Services.Interfaces;

public interface IDataImportService
{
    Task ImportFoodDataAsync(List<FddbFood> foods);
    Task<int> GetFoodCountAsync();
}