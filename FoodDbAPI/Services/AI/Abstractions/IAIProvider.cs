namespace FoodDbAPI.Services.AI.Abstractions;

/// <summary>
/// Core contract for an LLM provider. Implementations are fully responsible for
/// mapping the generic message/tool types to their own wire format.
/// The agent layer never imports any provider-specific types.
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// Sends a chat completion request and streams back events.
    /// The caller must iterate the returned <see cref="IAsyncEnumerable{T}"/> to
    /// drive the request; cancelling the token aborts the HTTP call.
    /// </summary>
    /// <param name="messages">Ordered conversation history, including the new user turn.</param>
    /// <param name="tools">Optional tools the model may invoke.</param>
    /// <param name="cancellationToken">Cancels the underlying streaming request.</param>
    IAsyncEnumerable<AIStreamEvent> StreamAsync(
        IList<AIMessage> messages,
        IList<AIToolDefinition>? tools = null,
        CancellationToken cancellationToken = default);
}
