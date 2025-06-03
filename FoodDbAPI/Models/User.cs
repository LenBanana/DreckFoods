using System.ComponentModel.DataAnnotations;

namespace FoodDbAPI.Models;

public class User
{
    public int Id { get; set; }

    [Required] [EmailAddress] public string Email { get; set; } = string.Empty;
    
    [Required] public AppRole Role { get; set; } = AppRole.User;

    [Required] public string PasswordHash { get; set; } = string.Empty;

    [Required] public string FirstName { get; set; } = string.Empty;

    [Required] public string LastName { get; set; } = string.Empty;

    public double? CurrentWeight { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<UserWeightEntry> WeightEntries { get; set; } = new List<UserWeightEntry>();
    public virtual ICollection<FoodEntry> FoodEntries { get; set; } = new List<FoodEntry>();
    
    // Email confirmation properties
    public bool   EmailConfirmed    { get; set; }
    public string? EmailTokenHash   { get; set; }
}