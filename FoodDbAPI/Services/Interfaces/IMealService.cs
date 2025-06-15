using FoodDbAPI.DTOs;

namespace FoodDbAPI.Services.Interfaces;

public interface IMealService
{
    Task<MealResponseDto> CreateMealAsync(int userId, CreateMealDto createMealDto);
    Task<MealResponseDto> GetMealByIdAsync(int mealId, int userId);
    Task<MealResponseDto> DuplicateMealAsync(int mealId, int userId);
    Task<List<MealResponseDto>> GetUserMealsAsync(int userId);
    Task<MealResponseDto> UpdateMealAsync(int mealId, int userId, UpdateMealDto updateMealDto);
    Task<bool> DeleteMealAsync(int mealId, int userId);
    Task<List<FoodEntryDto>> AddMealPortionAsync(int userId, AddMealPortionDto addMealPortionDto);
    string GetMealShareId(int mealId, int userId);
    Task<MealResponseDto> AddMealByShareIdAsync(string shareId, int userId);
}
