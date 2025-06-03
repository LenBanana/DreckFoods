using System.ComponentModel.DataAnnotations;

namespace FoodDbAPI.DTOs;

public class WeightEntryDto
{
    public int Id { get; set; }
    public double Weight { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateWeightEntryRequest
{
    [Required]
    [Range(0, 1000)]
    public double Weight { get; set; }

    [Required]
    public DateTime RecordedAt { get; set; }
}