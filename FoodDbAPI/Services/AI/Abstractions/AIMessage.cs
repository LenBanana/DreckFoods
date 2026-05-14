namespace FoodDbAPI.Services.AI.Abstractions;

/// <summary>
/// A single message in a conversation. Factory methods ensure correct construction
/// for each role so call sites stay readable.
/// </summary>
public class AIMessage
{
    public AIRole Role { get; init; }

    /// <summary>Text content. Required for System/User/Tool roles; optional for Assistant (may be null when model only returns tool calls).</summary>
    public string? Content { get; init; }

    /// <summary>Tool name. Required when Role = Tool so the model knows which call is being answered.</summary>
    public string? Name { get; init; }

    /// <summary>Required when Role = Tool — links this result back to a specific tool call.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Populated on assistant messages that include one or more tool calls.</summary>
    public List<AIToolCall>? ToolCalls { get; init; }

    // ── Factory methods ───────────────────────────────────────────────────────

    public static AIMessage System(string content)
        => new() { Role = AIRole.System, Content = content };

    public static AIMessage User(string content)
        => new() { Role = AIRole.User, Content = content };

    public static AIMessage Assistant(string? content, List<AIToolCall>? toolCalls = null)
        => new() { Role = AIRole.Assistant, Content = content, ToolCalls = toolCalls };

    /// <param name="toolCallId">The ID from the originating <see cref="AIToolCall"/>.</param>
    /// <param name="toolName">The function name — required by the OpenAI API.</param>
    /// <param name="content">Serialised result to return to the model.</param>
    public static AIMessage ToolResult(string toolCallId, string toolName, string content)
        => new() { Role = AIRole.Tool, ToolCallId = toolCallId, Name = toolName, Content = content };
}
