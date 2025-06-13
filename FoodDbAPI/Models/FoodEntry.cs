using System;
using System.ComponentModel.DataAnnotations;
using FoodDbAPI.DTOs.Base;
using FoodDbAPI.Models.Fddb;

namespace FoodDbAPI.Models;

public class FoodEntry : NutritionBase
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

    // Calculated nutrition values are now inherited from NutritionBase

    [Required]
    public DateTime ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual FddbFood FddbFood { get; set; } = null!;
}
