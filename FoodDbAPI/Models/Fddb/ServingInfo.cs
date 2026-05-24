namespace FoodDbAPI.Models.Fddb;

public class ServingInfo : IEquatable<ServingInfo>
{
    public string Name { get; set; } = string.Empty;
    public double WeightGrams { get; set; }

    public bool Equals(ServingInfo? other) =>
        other != null && Name == other.Name && WeightGrams == other.WeightGrams;

    public override bool Equals(object? obj) => obj is ServingInfo s && Equals(s);

    public override int GetHashCode() => HashCode.Combine(Name, WeightGrams);
}