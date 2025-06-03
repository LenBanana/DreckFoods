using System.ComponentModel.DataAnnotations;

namespace FoodDbAPI.Models;

public class UserWeightEntry
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [Range(0, 1000)]
    public double Weight { get; set; }

    [Required]
    public DateTime RecordedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual User User { get; set; } = null!;
}