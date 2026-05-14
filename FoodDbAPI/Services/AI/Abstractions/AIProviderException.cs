namespace FoodDbAPI.Services.AI.Abstractions;

/// <summary>
/// Thrown when an AI provider encounters an unrecoverable error
/// (e.g. authentication failure, rate-limit, network error).
/// </summary>
public class AIProviderException : Exception
{
    public AIProviderException(string message) : base(message) { }
    public AIProviderException(string message, Exception inner) : base(message, inner) { }
}
