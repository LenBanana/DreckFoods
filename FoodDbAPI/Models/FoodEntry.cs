using System.ComponentModel.DataAnnotations;
using FoodDbAPI.Models.Fddb;

namespace FoodDbAPI.Models;

public class FoodEntry
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }
    
    [Required]
    public int FddbFoodId { get; set; }

    [Required]
    public string FoodName { get; set; } = string.Empty;

    public string? FoodUrl { get; set; }
    public string? Brand { get; set; }
    public string? ImageUrl { get; set; }

    [Required]
    [Range(0.1, 10000)]
    public double GramsConsumed { get; set; }

    // Calculated nutrition values based on grams consumed
    public double Calories { get; set; }
    public double Protein { get; set; }
    public double Fat { get; set; }
    public double Carbohydrates { get; set; }
    public double Fiber { get; set; }
    public double Sugar { get; set; }

    [Required]
    public DateTime ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual FddbFood FddbFood { get; set; } = null!;
}