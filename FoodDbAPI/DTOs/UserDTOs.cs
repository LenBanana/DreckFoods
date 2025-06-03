using System.ComponentModel.DataAnnotations;

namespace FoodDbAPI.DTOs;

public class UserProfileDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public double? CurrentWeight { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateUserProfileRequest
{
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public double? CurrentWeight { get; set; }
}