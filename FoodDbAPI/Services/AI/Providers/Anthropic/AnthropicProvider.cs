using FoodDbAPI.Services.AI.Abstractions;

namespace FoodDbAPI.Services.AI.Providers.Anthropic;

/// <summary>
/// Placeholder for a future Anthropic Claude implementation of <see cref="IAIProvider"/>.
/// TODO: Implement using the Anthropic.SDK NuGet package when needed.
///       Register in Program.cs by replacing the OpenAIProvider binding.
/// </summary>
public class AnthropicProvider : IAIProvider
{
    public IAsyncEnumerable<AIStreamEvent> StreamAsync(
        IList<AIMessage> messages,
        IList<AIToolDefinition>? tools = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "Anthropic provider is not yet implemented. " +
            "See Services/AI/Providers/Anthropic/AnthropicProvider.cs to add it.");
    }
}
