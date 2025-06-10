using FoodDbAPI.DTOs;
using FoodDbAPI.Models.Fddb;

namespace FoodDbAPI.Services.Interfaces;

public interface IFddbEditorService
{
    Task<FoodSearchDto?> GetFoodByIdAsync(int id);
    Task<bool> UpdateFoodInfoAsync(int id, FddbFoodUpdateDTO? updateDto);
    Task<bool> UpdateFoodNutritionAsync(int id, FddbFoodNutritionUpdateDTO? updateDto);
    Task<bool> UpdateFoodCompleteAsync(int id, FddbFoodCompleteUpdateDTO? updateDto);
}
