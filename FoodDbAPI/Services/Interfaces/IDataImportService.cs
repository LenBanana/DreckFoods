using FoodDbAPI.DTOs;
using FoodDbAPI.Models.Fddb;

namespace FoodDbAPI.Services.Interfaces;

public interface IDataImportService
{
    Task ImportFoodDataAsync(List<FddbFoodImportDTO> foods);
    Task<int> GetFoodCountAsync();
}