namespace FoodDbAPI.Services.AI.Abstractions;

/// <summary>
/// Represents a tool call requested by the model.
/// </summary>
public class AIToolCall
{
    public required string Id { get; init; }
    public required string FunctionName { get; init; }
    public required string ArgumentsJson { get; init; }
}
