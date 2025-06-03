using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FoodDbAPI.Models.Fddb;

public class FddbFood
{
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string ImageUrl { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Brand { get; set; } = string.Empty;

    // Store tags as JSON string
    public string TagsJson { get; set; } = "[]";

    [NotMapped]
    public List<string> Tags
    {
        get => string.IsNullOrEmpty(TagsJson) ? new List<string>() : 
            System.Text.Json.JsonSerializer.Deserialize<List<string>>(TagsJson) ?? new List<string>();
        set => TagsJson = System.Text.Json.JsonSerializer.Serialize(value);
    }

    // Navigation property for nutrition
    public virtual FddbFoodNutrition Nutrition { get; set; } = new();
}