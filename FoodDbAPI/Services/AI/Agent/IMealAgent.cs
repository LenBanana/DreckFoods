namespace FoodDbAPI.Services.AI.Agent;

public interface IMealAgent
{
    IAsyncEnumerable<AgentEvent> ChatAsync(
        Guid sessionId,
        string userMessage,
        CancellationToken cancellationToken = default);
}
