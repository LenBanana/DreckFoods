using System.ComponentModel.DataAnnotations;
using FoodDbAPI.Models.Fddb;

namespace FoodDbAPI.Models;

public class MealItem
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int MealId { get; set; }
    
    public Meal? Meal { get; set; }
    
    [Required]
    public int FddbFoodId { get; set; }
    
    public FddbFood? FddbFood { get; set; }
    
    [Required]
    public double Weight { get; set; } // In grams
}
