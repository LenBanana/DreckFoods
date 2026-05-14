namespace FoodDbAPI.Services.AI.Abstractions;

/// <summary>
/// Discriminated union of events that can be emitted during a single streaming
/// API call. Consumers iterate with pattern matching (switch expression).
/// </summary>
public abstract record AIStreamEvent;

/// <summary>Incremental text token from the model.</summary>
public record TextChunkEvent(string Text) : AIStreamEvent;

/// <summary>
/// A tool call that the model has fully assembled (all argument fragments concatenated).
/// Emitted once per tool call when the stream signals finish_reason = tool_calls.
/// </summary>
public record ToolCallCompletedEvent(
    string Id,
    string FunctionName,
    string ArgumentsJson) : AIStreamEvent;

/// <summary>
/// The current streaming call is finished (either text stop or after tool calls).
/// Consumers should stop reading and act on any buffered tool calls.
/// </summary>
public record StreamDoneEvent : AIStreamEvent;
