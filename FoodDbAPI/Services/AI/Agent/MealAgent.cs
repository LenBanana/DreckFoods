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
/// multiple internal model->tool->model round-trips before a final text response
/// or a UI-driven tool call (suggest_food / ask_questions) that ends the turn.
/// </summary>
public class MealAgent : IMealAgent
{
    private const int MaxIterations = 30;

    /// <summary>
    /// System prompt injected at the start of every turn.
    /// {PAST_FOODS} is replaced at runtime with the user's recent food history.
    /// </summary>
    private const string SystemPromptTemplate =
        """
        You are a meal logging assistant for a nutrition tracking app.
        Your single goal: log the user's meal as accurately as possible.
        The user describes what they ate in free text, your goal is to identify each ingredient, its quantity in grams, and the best-matching food in the database.
        When users describe a meal, they often omit details like brand names or quantities. Use the tools below to ask follow-up questions and search the food database until you're confident about each ingredient's identity and quantity.
        Sometimes users may eat out or consume new products that aren't in the database. In that case, do your best to find similar foods and ask questions to narrow down the closest match. It's better to ask too many questions than to log something inaccurate.
        Users are optimizing their health, so they care about accurate calorie and macronutrient counts. When in doubt, ask questions or offer multiple candidates instead of guessing wrong.

        === TOOLS ===
        * ask_questions     -- Total weight when genuinely unknown. Set recommended:true on your best guess.
        * search_food       -- Generic term, refine until satisfied. No search limit.
        * suggest_food      -- All ingredients at once. Per-ingredient weights. Always preselect best match.
        * update_meal_draft -- After confirmation or via DIRECT PATH.

        === PREVIOUSLY CONSUMED FOODS ===
        The foods below were recently eaten by this user. Their food_ids are valid.
        You MAY include them as candidates in suggest_food or use them in the DIRECT PATH without searching.
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
                // Pure text response -- turn is complete.
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

                // -- search_food ---------------------------------------------------
                if (toolCall.FunctionName == MealAgentTools.SearchFood.Name)
                {
                    var args = JsonSerializer.Deserialize<SearchFoodArgs>(toolCall.ArgumentsJson, JsonOptions)
                        ?? throw new InvalidOperationException("Failed to deserialise search_food arguments.");

                    yield return new AgentToolSearching(args.Query);

                    FoodSearchResponse? results = null;
                    try
                    {
                        results = await _foodSearchService.SearchFoodsAsync(
                            query: args.Query,
                            userId: session.UserId,
                            page: 1,
                            pageSize: 50,
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

                        // Return a compact summary to avoid wasting tokens.
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
                            ? "No results found. Try a broader or different generic term."
                            : JsonSerializer.Serialize(modelContext);
                    }
                }

                // -- suggest_food --------------------------------------------------
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
                            AllowCustom = suggestion.AllowCustom ?? true,
                            PreselectedFoodId = suggestion.PreselectedFoodId
                        });
                    }

                    yield return new AgentSuggestFood(groups);

                    toolResult = "Selection widget displayed. Your turn ends here. " +
                                 "Do NOT write any follow-up text. The user will send '[Confirmed food selections]' in their next message.";
                    endTurnAfterTools = true;
                }

                // -- ask_questions -------------------------------------------------
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
                            Value = c.Value,
                            Recommended = c.Recommended
                        }).ToList()
                    }).ToList();

                    yield return new AgentAskQuestions(questions);

                    toolResult = "Question widget displayed. Your turn ends here. " +
                                 "Do NOT write any follow-up text. The user will send '[Answers to questions]' in their next message.";
                    endTurnAfterTools = true;
                }

                // -- update_meal_draft ---------------------------------------------
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

            // For UI-ending tools the turn is complete -- the user will send a new message.
            if (endTurnAfterTools)
            {
                yield return new AgentDone();
                yield break;
            }
        }

        yield return new AgentError("Maximum agent iteration limit reached without a final response.");
    }

    // -- Dynamic system prompt ---------------------------------------------------

    private async Task<string> BuildSystemPromptAsync(int userId, CancellationToken ct)
    {
        string pastFoodsSection;

        try
        {
            var pastFoods = await _foodSearchService.GetPastEatenFoodsAsync(userId, 1, 15);

            if (pastFoods.Foods.Count == 0)
            {
                pastFoodsSection = "(No history yet -- use search_food for every ingredient.)";
            }
            else
            {
                var lines = pastFoods.Foods.Select(f =>
                {
                    var brand = string.IsNullOrWhiteSpace(f.Brand) ? "" : $" ({f.Brand})";
                    return $"- {f.Name}{brand} [food_id: {f.Id}] -- " +
                           $"{f.Nutrition.Calories.Value:F0} kcal, {f.Nutrition.Protein.Value:F1}g P, " +
                           $"{f.Nutrition.Carbohydrates.Total.Value:F1}g C, {f.Nutrition.Fat.Value:F1}g F (per 100g)";
                });

                pastFoodsSection = string.Join("\n", lines);
            }
        }
        catch
        {
            pastFoodsSection = "(History unavailable -- use search_food as usual.)";
        }

        return SystemPromptTemplate.Replace("{PAST_FOODS}", pastFoodsSection);
    }

    // -- Private DTO types for tool argument deserialisation ---------------------

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

        [JsonPropertyName("preselected_food_id")]
        public int? PreselectedFoodId { get; set; }

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

        [JsonPropertyName("recommended")]
        public bool Recommended { get; set; }
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
