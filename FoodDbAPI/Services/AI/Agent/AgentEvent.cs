using FoodDbAPI.DTOs;

namespace FoodDbAPI.Services.AI.Agent;

/// <summary>
/// Discriminated union of events yielded by <see cref="IMealAgent.ChatAsync"/>.
/// The controller maps each event to its corresponding SSE payload.
/// </summary>
public abstract record AgentEvent;

/// <summary>Incremental text token to stream to the client.</summary>
public record AgentTextChunk(string Text) : AgentEvent;

/// <summary>A tool call has started; the client should show a "Searching…" indicator.</summary>
public record AgentToolSearching(string Query) : AgentEvent;

/// <summary>Food search results (for activity log only; use AgentSuggestFood for selection UI).</summary>
public record AgentFoodResults(string Query, List<FoodSearchDto> Foods) : AgentEvent;

/// <summary>The meal draft was updated; the client should refresh the draft panel.</summary>
public record AgentMealDraftUpdated(List<MealDraftItem> Items) : AgentEvent;

/// <summary>The complete assistant turn text, for persisting in chat history.</summary>
public record AgentAssistantMessage(string Text) : AgentEvent;

/// <summary>The current turn is complete; the client should re-enable the input.</summary>
public record AgentDone : AgentEvent;

/// <summary>An unrecoverable error occurred; the client should surface the message.</summary>
public record AgentError(string Message) : AgentEvent;

/// <summary>
/// Instructs the client to show a structured food-selection widget.
/// The agent calls <c>suggest_food</c> after completing all searches.
/// The turn ends after this event — the agent waits for the user's next message.
/// </summary>
public record AgentSuggestFood(List<FoodSuggestionGroup> Suggestions) : AgentEvent;

/// <summary>
/// Instructs the client to show a structured question widget (e.g., for missing quantities).
/// The turn ends after this event — the agent waits for the user's next message.
/// </summary>
public record AgentAskQuestions(List<AgentQuestion> Questions) : AgentEvent;

// ── Supporting types for AgentSuggestFood ────────────────────────────────────

/// <summary>One ingredient with its food candidates, ready for UI selection.</summary>
public class FoodSuggestionGroup
{
    public string IngredientLabel { get; init; } = string.Empty;
    public double QuantityGrams { get; init; }
    public List<FoodSearchDto> Candidates { get; init; } = [];
    public bool AllowCustom { get; init; } = true;
}

// ── Supporting types for AgentAskQuestions ───────────────────────────────────

/// <summary>A single clarifying question with optional preset choices.</summary>
public class AgentQuestion
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public List<AgentQuestionChoice>? Choices { get; init; }
    public bool AllowCustom { get; init; } = true;
}

/// <summary>One selectable answer option for an <see cref="AgentQuestion"/>.</summary>
public class AgentQuestionChoice
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}
