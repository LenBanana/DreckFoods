using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FoodDbAPI.DTOs;
using FoodDbAPI.DTOs.Enums;
using FoodDbAPI.Services.AI.Abstractions;
using FoodDbAPI.Services.AI.Sessions;
using FoodDbAPI.Services.Interfaces;

namespace FoodDbAPI.Services.AI.Agent;

/// <summary>
/// Orchestrates a multi-turn, tool-calling conversation loop for meal logging.
/// Each call to <see cref="ChatAsync"/> runs one user turn, which may involve
/// multiple internal modelâ†’toolâ†’model round-trips before a final text response
/// or a UI-driven tool call (suggest_food / ask_questions) that ends the turn.
/// </summary>
public class MealAgent : IMealAgent
{
    private const int MaxIterations = 15;

    /// <summary>
    /// Base system prompt. {PAST_FOODS} is replaced at runtime with the user's
    /// recent food history so the agent can reference known food_ids directly.
    /// </summary>
    private const string SystemPromptTemplate =
        """
        You are a meal logging assistant for a nutrition tracking app.
        Your single goal: log the user's meal accurately and with as little back-and-forth as possible.

        â•â•â• LANGUAGE â•â•â•
        Always reply in the same language the user writes in.
        German input â†’ German response. English input â†’ English response. Never switch mid-session.

        â•â•â• SEARCH STRATEGY (read carefully) â•â•â•
        Always search with GENERIC ingredient names. Strip brands, preparation methods, and adjectives.
        Examples of correct translations:
          "Beutelreis (125g gekocht)"  â†’ search "Reis"  OR "Langkornreis"
          "HÃ¼hner Filetsteak"          â†’ search "HÃ¤hnchenfilet"
          "frische Champignons"        â†’ search "Champignons"
          "Milch 3,5%"                 â†’ search "Vollmilch"
          "1 Ei (L)"                   â†’ search "HÃ¼hnerei"
          "Sojasauce"                  â†’ search "Sojasauce"
          "Haferflocken (Marzipan)"    â†’ search "Haferflocken"

        Per ingredient: ONE search. If you get fewer than 2 relevant results, try ONE broader alternative.
        Never search the same ingredient more than twice. Never call search_food after suggest_food.
        EXCEPTION: When processing [needs search] items, use the user's quoted text verbatim (see HANDLING section below). The generic-name rule does NOT apply there.

        â•â•â• WORKFLOW â•â•â•
        1.  Parse every food item and its quantity from the user's message.
        2.  If any quantity is missing â†’ call ask_questions first (before searching).
        3.  Call search_food for each ingredient (generic terms, one call per ingredient).
        4.  Call suggest_food with ALL ingredients grouped together (one call, not per-ingredient).
        5.  After the user responds with "[Confirmed food selections]" â†’ call update_meal_draft
            with the listed food_ids and weights immediately. Do not search again.
        6.  After the user responds with "[Answers to questions]" â†’ apply the quantities,
            proceed from step 3.

        â•â•â• TOOLS â•â•â•
        â€¢ ask_questions  â€“ Use BEFORE searching for missing quantities. Provide 4-6 sensible choices
                           with gram equivalents (e.g. "1 EL (15g)", "1 Tasse (240ml)").
        â€¢ search_food    â€“ One generic search per ingredient.
        â€¢ suggest_food   â€“ Show ALL ingredients with candidates at once. Call once, after all searches.
                           Do NOT write any text message before or after calling suggest_food.
        â€¢ update_meal_draft â€“ Call immediately after receiving "[Confirmed food selections]".
                              Include ALL items, not just new ones.

        HANDLING "[needs search]"
        When the user's "[Confirmed food selections]" contains an item tagged "[needs search]",
        the item format is:  - Label: "user text" (Xg) [needs search]
        The text in quotes is what the user typed. Use it VERBATIM as the search query.
        Do NOT generalise to a generic ingredient name - the user chose specific text because
        the generic search already failed them. Examples:
          "basmati reis uncle bens" (125g) [needs search]  -> search query = "basmati reis uncle bens"
          "champignons frisch" (150g) [needs search]       -> search query = "champignons frisch"
          "kikoman sojasauce" (5g) [needs search]          -> search query = "kikoman sojasauce"
        After searching all "[needs search]" items, call suggest_food for those items only.
        Items that already have a food_id are resolved - include them in update_meal_draft as-is.

        â•â•â• PREVIOUSLY CONSUMED FOODS â•â•â•
        The foods below were recently eaten by this user. Their food_ids are valid.
        You MAY include them directly as candidates in suggest_food without calling search_food first.
        Prefer these when the user's description matches.

        {PAST_FOODS}
        """;

    private readonly IAIProvider _aiProvider;
    private readonly IMealSessionStore _sessionStore;
    private readonly IFoodSearchService _foodSearchService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MealAgent(
        IAIProvider aiProvider,
        IMealSessionStore sessionStore,
        IFoodSearchService foodSearchService)
    {
        _aiProvider = aiProvider;
        _sessionStore = sessionStore;
        _foodSearchService = foodSearchService;
    }

    public async IAsyncEnumerable<AgentEvent> ChatAsync(
        Guid sessionId,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var session = _sessionStore.GetSession(sessionId)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

        session.Messages.Add(AIMessage.User(userMessage));

        // Build system prompt once per turn (contains fresh past-foods context).
        var systemPrompt = await BuildSystemPromptAsync(session.UserId, cancellationToken);

        var messages = new List<AIMessage>(session.Messages.Count + 1)
        {
            AIMessage.System(systemPrompt)
        };
        messages.AddRange(session.Messages);

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var accumulatedText = new StringBuilder();
            var pendingToolCalls = new List<ToolCallCompletedEvent>();

            // Stream one provider turn. Text chunks are yielded inline; tool calls are buffered.
            await foreach (var ev in _aiProvider.StreamAsync(messages, MealAgentTools.All, cancellationToken))
            {
                switch (ev)
                {
                    case TextChunkEvent textChunk:
                        accumulatedText.Append(textChunk.Text);
                        yield return new AgentTextChunk(textChunk.Text);
                        break;

                    case ToolCallCompletedEvent toolCall:
                        pendingToolCalls.Add(toolCall);
                        break;

                    case StreamDoneEvent:
                        break;
                }
            }

            if (pendingToolCalls.Count == 0)
            {
                // Pure text response â€” turn is complete.
                var finalText = accumulatedText.ToString();
                session.Messages.Add(AIMessage.Assistant(finalText));
                _sessionStore.UpdateSession(session);
                yield return new AgentAssistantMessage(finalText);
                yield return new AgentDone();
                yield break;
            }

            // Append the assistant turn (with tool calls) to the working message list.
            var toolCallList = pendingToolCalls
                .Select(tc => new AIToolCall
                {
                    Id = tc.Id,
                    FunctionName = tc.FunctionName,
                    ArgumentsJson = tc.ArgumentsJson
                })
                .ToList();

            messages.Add(AIMessage.Assistant(
                accumulatedText.Length > 0 ? accumulatedText.ToString() : null,
                toolCallList));

            // Process each tool call. endTurnAfterTools = true for UI-ending tools.
            var endTurnAfterTools = false;

            foreach (var toolCall in pendingToolCalls)
            {
                string toolResult;

                // â”€â”€ search_food â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                if (toolCall.FunctionName == MealAgentTools.SearchFood.Name)
                {
                    var args = JsonSerializer.Deserialize<SearchFoodArgs>(toolCall.ArgumentsJson, JsonOptions)
                        ?? throw new InvalidOperationException("Failed to deserialise search_food arguments.");

                    yield return new AgentToolSearching(args.Query);

                    // Wrap the search so a transient backend failure (DB unavailable, scrape
                    // error, etc.) produces a graceful tool result instead of crashing the stream.
                    FoodSearchResponse? results = null;
                    try
                    {
                        results = await _foodSearchService.SearchFoodsAsync(
                            query: args.Query,
                            userId: session.UserId,
                            page: 1,
                            pageSize: 7,
                            sortBy: FoodSortBy.Name,
                            sortDirection: SortDirection.Ascending);
                    }
                    catch (Exception)
                    {
                        // FoodSearchService already logs the underlying error.
                    }

                    if (results == null)
                    {
                        toolResult = "Search temporarily unavailable. Try a different term or proceed with known food_ids.";
                    }
                    else
                    {
                        // Cache results for suggest_food and update_meal_draft lookups.
                        foreach (var food in results.Foods)
                            session.FoodCache[food.Id] = food;

                        yield return new AgentFoodResults(args.Query, results.Foods);

                        // Return a compact summary to the model to avoid wasting tokens.
                        var modelContext = results.Foods.Select(f => new
                        {
                            id = f.Id,
                            name = f.Name,
                            brand = f.Brand,
                            calories_per_100g = f.Nutrition.Calories.Value,
                            protein_per_100g = f.Nutrition.Protein.Value,
                            carbs_per_100g = f.Nutrition.Carbohydrates.Total.Value,
                            fat_per_100g = f.Nutrition.Fat.Value,
                            previously_eaten = f.PreviouslyEaten
                        });

                        toolResult = results.Foods.Count == 0
                            ? "No results found. Try a broader generic term."
                            : JsonSerializer.Serialize(modelContext);
                    }
                }

                // â”€â”€ suggest_food â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                else if (toolCall.FunctionName == MealAgentTools.SuggestFood.Name)
                {
                    var args = JsonSerializer.Deserialize<SuggestFoodArgs>(toolCall.ArgumentsJson, JsonOptions)
                        ?? throw new InvalidOperationException("Failed to deserialise suggest_food arguments.");

                    var groups = new List<FoodSuggestionGroup>();

                    foreach (var suggestion in args.Suggestions)
                    {
                        var candidates = new List<FoodSearchDto>();

                        foreach (var foodId in suggestion.CandidateFoodIds)
                        {
                            // Try session cache first (populated by search_food), then DB.
                            if (!session.FoodCache.TryGetValue(foodId, out var food))
                                food = await _foodSearchService.GetFoodByIdAsync(foodId);

                            if (food != null)
                            {
                                session.FoodCache[food.Id] = food;
                                candidates.Add(food);
                            }
                        }

                        groups.Add(new FoodSuggestionGroup
                        {
                            IngredientLabel = suggestion.IngredientLabel,
                            QuantityGrams = suggestion.QuantityGrams,
                            Candidates = candidates,
                            AllowCustom = suggestion.AllowCustom ?? true
                        });
                    }

                    yield return new AgentSuggestFood(groups);

                    toolResult = "Selection widget displayed. Your turn ends here. " +
                                 "Do NOT write any follow-up text. The user will send '[Confirmed food selections]' in their next message.";
                    endTurnAfterTools = true;
                }

                // â”€â”€ ask_questions â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                else if (toolCall.FunctionName == MealAgentTools.AskQuestions.Name)
                {
                    var args = JsonSerializer.Deserialize<AskQuestionsArgs>(toolCall.ArgumentsJson, JsonOptions)
                        ?? throw new InvalidOperationException("Failed to deserialise ask_questions arguments.");

                    var questions = args.Questions.Select(q => new AgentQuestion
                    {
                        Id = q.Id,
                        Label = q.Label,
                        AllowCustom = q.AllowCustom ?? true,
                        Choices = q.Choices?.Select(c => new AgentQuestionChoice
                        {
                            Label = c.Label,
                            Value = c.Value
                        }).ToList()
                    }).ToList();

                    yield return new AgentAskQuestions(questions);

                    toolResult = "Question widget displayed. Your turn ends here. " +
                                 "Do NOT write any follow-up text. The user will send '[Answers to questions]' in their next message.";
                    endTurnAfterTools = true;
                }

                // â”€â”€ update_meal_draft â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
                else if (toolCall.FunctionName == MealAgentTools.UpdateMealDraft.Name)
                {
                    var args = JsonSerializer.Deserialize<UpdateMealDraftArgs>(toolCall.ArgumentsJson, JsonOptions)
                        ?? throw new InvalidOperationException("Failed to deserialise update_meal_draft arguments.");

                    yield return new AgentToolSearching("Mahlzeit-Entwurf");

                    var draftItems = new List<MealDraftItem>();

                    foreach (var item in args.Items)
                    {
                        session.FoodCache.TryGetValue(item.FoodId, out var food);
                        food ??= await _foodSearchService.GetFoodByIdAsync(item.FoodId);

                        draftItems.Add(new MealDraftItem
                        {
                            FddbFoodId = item.FoodId,
                            FoodName = item.FoodName,
                            WeightGrams = item.WeightGrams,
                            ImageUrl = food?.ImageUrl,
                            Nutrition = food?.Nutrition
                        });
                    }

                    session.MealDraft = draftItems;
                    yield return new AgentMealDraftUpdated(draftItems);

                    toolResult = "Draft updated successfully.";
                }

                else
                {
                    toolResult = $"Unknown tool '{toolCall.FunctionName}'.";
                }

                messages.Add(AIMessage.ToolResult(toolCall.Id, toolCall.FunctionName, toolResult));
            }

            // Sync session history (skip the system prompt at index 0).
            session.Messages = messages.Skip(1).ToList();
            _sessionStore.UpdateSession(session);

            // For UI-ending tools the turn is complete â€” the user will send a new message.
            if (endTurnAfterTools)
            {
                yield return new AgentDone();
                yield break;
            }
        }

        yield return new AgentError("Maximum agent iteration limit reached without a final response.");
    }

    // â”€â”€ Dynamic system prompt â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async Task<string> BuildSystemPromptAsync(int userId, CancellationToken ct)
    {
        string pastFoodsSection;

        try
        {
            var pastFoods = await _foodSearchService.GetPastEatenFoodsAsync(userId, 1, 15);

            if (pastFoods.Foods.Count == 0)
            {
                pastFoodsSection = "(No history yet â€” use search_food for every ingredient.)";
            }
            else
            {
                var lines = pastFoods.Foods.Select(f =>
                {
                    var brand = string.IsNullOrWhiteSpace(f.Brand) ? "" : $" ({f.Brand})";
                    return $"- {f.Name}{brand} [food_id: {f.Id}] â€“ " +
                           $"{f.Nutrition.Calories.Value:F0} kcal, {f.Nutrition.Protein.Value:F1}g P, " +
                           $"{f.Nutrition.Carbohydrates.Total.Value:F1}g C, {f.Nutrition.Fat.Value:F1}g F (per 100g)";
                });

                pastFoodsSection = string.Join("\n", lines);
            }
        }
        catch
        {
            pastFoodsSection = "(History unavailable â€” use search_food as usual.)";
        }

        return SystemPromptTemplate.Replace("{PAST_FOODS}", pastFoodsSection);
    }

    // â”€â”€ Private DTO types for tool argument deserialisation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private sealed class SearchFoodArgs
    {
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;
    }

    private sealed class SuggestFoodArgs
    {
        [JsonPropertyName("suggestions")]
        public List<SuggestionArg> Suggestions { get; set; } = [];
    }

    private sealed class SuggestionArg
    {
        [JsonPropertyName("ingredient_label")]
        public string IngredientLabel { get; set; } = string.Empty;

        [JsonPropertyName("quantity_grams")]
        public double QuantityGrams { get; set; }

        [JsonPropertyName("candidate_food_ids")]
        public List<int> CandidateFoodIds { get; set; } = [];

        [JsonPropertyName("allow_custom")]
        public bool? AllowCustom { get; set; }
    }

    private sealed class AskQuestionsArgs
    {
        [JsonPropertyName("questions")]
        public List<QuestionArg> Questions { get; set; } = [];
    }

    private sealed class QuestionArg
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("choices")]
        public List<ChoiceArg>? Choices { get; set; }

        [JsonPropertyName("allow_custom")]
        public bool? AllowCustom { get; set; }
    }

    private sealed class ChoiceArg
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    private sealed class UpdateMealDraftArgs
    {
        [JsonPropertyName("items")]
        public List<DraftItemArg> Items { get; set; } = [];
    }

    private sealed class DraftItemArg
    {
        [JsonPropertyName("food_id")]
        public int FoodId { get; set; }

        [JsonPropertyName("food_name")]
        public string FoodName { get; set; } = string.Empty;

        [JsonPropertyName("weight_grams")]
        public double WeightGrams { get; set; }
    }
}
