using System.Text.Json;

namespace FoodDbAPI.Services.AI.Abstractions;

/// <summary>
/// Provider-agnostic definition of a tool (function) the model can call.
/// The parameter schema must be a valid JSON Schema object.
/// </summary>
public class AIToolDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary>
    /// JSON Schema for the function parameters, e.g.
    /// { "type": "object", "properties": { ... }, "required": [...] }
    /// </summary>
    public required JsonElement ParametersJsonSchema { get; init; }
}
