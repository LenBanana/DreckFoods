using FoodDbAPI.Models.Fddb;

namespace FoodDbAPI.DTOs;

public class FddbFoodImportDto
{
    public string Name { get; set; }
    public string Url { get; set; }
    public string Description { get; set; }
    public string ImageUrl { get; set; }
    public string Brand { get; set; }
    public string? Ean { get; set; }
    public List<string> Tags { get; set; } = new();
    public NutritionInfo Nutrition { get; set; } = new();

    public static FddbFood MapImportDtoToEntity(FddbFoodImportDto dto)
    {
        return new FddbFood
        {
            Name = dto.Name,
            Url = dto.Url,
            Description = dto.Description,
            ImageUrl = dto.ImageUrl,
            Brand = dto.Brand,
            Ean = dto.Ean,
            Tags = dto.Tags,
            Nutrition = FddbFoodNutrition.FromNutritionInfo(dto.Nutrition)
        };
    }
}