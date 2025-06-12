using FoodDbAPI.DTOs;
using FoodDbAPI.DTOs.Enums;

namespace FoodDbAPI.Services.Interfaces;

public interface IFoodSearchService
{
    Task<FoodSearchResponse> SearchFoodsAsync(
        string query, 
        int? userId = null,
        int page = 1, 
        int pageSize = 20,
        FoodSortBy sortBy = FoodSortBy.Name,
        SortDirection sortDirection = SortDirection.Ascending);
    
    Task<List<string>> GetFoodCategoriesAsync();
    Task<FoodSearchDto?> GetFoodByIdAsync(int foodId);
    Task<FoodSearchResponse> GetPastEatenFoodsAsync(int userId, int page = 1, int pageSize = 20);
}
