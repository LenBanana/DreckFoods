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
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
