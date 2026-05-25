using System.ComponentModel.DataAnnotations;

namespace FoodDbAPI.Models;

public class MealPortionLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    public User User { get; set; } = null!;

    [Required]
    public int MealId { get; set; }

    public Meal Meal { get; set; } = null!;

    [Required]
    [Range(0.1, 10000)]
    public double Weight { get; set; }

    [MaxLength(200)]
    public string? ServingName { get; set; }

    [Required]
    public DateTime ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}