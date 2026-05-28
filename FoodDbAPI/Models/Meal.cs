using System.ComponentModel.DataAnnotations;

namespace FoodDbAPI.Models;

public class Meal
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    public int UserId { get; set; }
    
    public User? User { get; set; }
    
    public ICollection<MealItem> MealItems { get; set; } = new List<MealItem>();
    
    /// <summary>
    /// Optional cooked/final weight of the meal in grams. When set, macro concentration
    /// per 100 g is calculated using this value instead of the sum of raw ingredient weights.
    /// </summary>
    public double? CookedWeight { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
