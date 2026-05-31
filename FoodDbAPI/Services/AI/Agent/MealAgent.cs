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
    /// System prompt for "log" mode — injected at the start of every turn.
    /// {PAST_FOODS} is replaced at runtime with the user's recent food history.
    /// </summary>
    private const string LogModeSystemPromptTemplate =
        """
        You are a meal logging assistant for a nutrition tracking app.
        Your goal is to log the user's meal as accurately and efficiently as possible.
        Identify every food item, the consumed grams, and the best matching food_id in the database.
        Users care about calories and macros, so product identity, preparation state, and grams matter.

        === SEARCH STRATEGY ===
        The food database search matches every query word against food name, brand, EAN/barcode, and description.
        Use the most specific useful query first. If the user gives a brand, product line, barcode, flavor, or preparation state, include it.
        Use generic ingredient names only when the user described a generic ingredient, or when an exact product search fails.
        If results are weak or empty, retry with fewer/different distinctive words; remove package-size words, marketing adjectives, or spelling variants before giving up.
        If results are too broad, retry with brand, product type, preparation state, or other distinctive terms.
        Prefer an exact brand/product match over a generic food. Prefer previously consumed foods when the user description is ambiguous or matches their history.
        Use serving_hints from search results to convert portions like "1 Becher", "2 Scheiben", or the user's usual serving into grams.

        Ask questions only when the missing answer materially changes the food choice or grams. Provide realistic preset answers and mark the best estimate as recommended.
        When several plausible foods remain, use suggest_food with the best candidate preselected so the user can review quickly.
        DIRECT PATH: if food_ids and grams are already certain (for example from a confirmed selection, exact previous food, or unambiguous exact search result), update the meal draft directly.

        === TOOLS ===
        * ask_questions     -- Clarify grams, brand/product, preparation state, or key missing details. Set recommended:true on your best estimate.
        * search_food       -- Search exact brand/product/barcode terms when available; broaden or narrow based on results.
        * suggest_food      -- Present all unresolved food choices at once. Per-item grams. Always preselect the best match when one exists.
        * update_meal_draft -- Replace the draft after confirmation, or via DIRECT PATH when no meaningful user choice remains.

        === PREVIOUSLY CONSUMED FOODS ===
        The foods below were recently eaten by this user. Their food_ids are valid.
        You MAY include them as candidates in suggest_food or use them in the DIRECT PATH without searching.
        Prefer these when the user's description matches.

        {PAST_FOODS}
        """;

    /// <summary>
    /// System prompt for "plan" mode — the agent acts as a recipe builder / meal planner.
    /// The draft is saved as a reusable meal template; nothing is logged immediately.
    /// {PAST_FOODS} is replaced at runtime with the user's known foods.
    /// </summary>
    private const string PlanModeSystemPromptTemplate =
        """
        You are a meal planning and recipe assistant for a nutrition tracking app.
        Your role is to help users explore meal ideas, suggest complete recipes, and build a full ingredient list that will be saved as a reusable meal template — NOT logged immediately.

        When the user asks for meal ideas or describes a dish they want to make:
        1. Be proactive: suggest a complete, balanced set of ingredients with realistic quantities.
        2. Ask clarifying questions only when the serving size or a key ingredient is genuinely unclear.
        3. Search the database for every ingredient. Use brands/product names when the user gives them; otherwise use specific ingredient names.
        4. Present all candidates at once via suggest_food -- always preselect your best match so the user only needs to review.
        5. After the user confirms, update the recipe draft. You may follow up to refine the recipe or add sides.

        The final draft is saved as a reusable recipe. The user will log individual portions of it later — you do NOT need to ask when it was consumed.

        Be flexible and creative. The user may:
        - Ask for a specific dish ("Help me build a chili recipe")
        - Ask for inspiration ("What's a good high-protein lunch?")
        - Describe a meal they plan to cook ("I want to make pasta bolognese for 2")
        - Want to save a recipe they already know by heart

        For serving size: assume one person unless stated otherwise. Use standard cooking quantities.
        Always think about balance: protein, carbohydrates, fat, and vegetables.

        === SEARCH STRATEGY ===
        The food database search matches every query word against food name, brand, EAN/barcode, and description.
        Use exact brand/product queries for packaged foods and specific ingredient queries for whole foods.
        If results are weak or empty, retry with fewer/different distinctive words. If results are too broad, add brand, preparation state, or product type.
        Prefer exact product matches over generic foods. Prefer known foods for this user when relevant.
        Use serving_hints from search results when converting recipe units or usual portions into grams.

        === TOOLS ===
        * ask_questions     -- Ask about serving count, missing ingredients, dietary preferences, or grams. Set recommended:true on your best estimate.
        * search_food       -- Search exact brand/product/barcode terms when available; broaden or narrow based on results.
        * suggest_food      -- Present all unresolved food choices at once. Per-item quantities. Always preselect your best match.
        * update_meal_draft -- After the user confirms selections.

        === KNOWN FOODS FOR THIS USER ===
        The foods below have been consumed or saved by this user. Their food_ids are valid.
        Prefer these as candidates in suggest_food when relevant.

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
        var systemPrompt = await BuildSystemPromptAsync(session.UserId, session.Mode, cancellationToken);

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
                        var rankedFoods = RankFoodsForAgent(args.Query, results.Foods);

                        // Cache results for suggest_food and update_meal_draft lookups.
                        foreach (var food in rankedFoods)
                            session.FoodCache[food.Id] = food;

                        yield return new AgentFoodResults(args.Query, rankedFoods);

                        // Return a compact summary to avoid wasting tokens.
                        var modelContext = rankedFoods.Select(food => new
                        {
                            id = food.Id,
                            name = food.Name,
                            brand = food.Brand,
                            ean = food.Ean,
                            calories_per_100g = food.Nutrition.Calories.Value,
                            protein_per_100g = food.Nutrition.Protein.Value,
                            carbs_per_100g = food.Nutrition.Carbohydrates.Total.Value,
                            fat_per_100g = food.Nutrition.Fat.Value,
                            serving_hints = food.Servings.Take(4).Select(serving => new
                            {
                                name = serving.Name,
                                grams = serving.WeightGrams
                            }),
                            previously_eaten = food.PreviouslyEaten
                        });

                        toolResult = rankedFoods.Count == 0
                            ? "No results found. Try an exact brand/product/barcode if known, or retry with fewer/different distinctive words."
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

    private async Task<string> BuildSystemPromptAsync(int userId, string mode, CancellationToken ct)
    {
        var template = mode == "plan" ? PlanModeSystemPromptTemplate : LogModeSystemPromptTemplate;
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

        return template.Replace("{PAST_FOODS}", pastFoodsSection);
    }

    private static List<FoodSearchDto> RankFoodsForAgent(string query, IReadOnlyList<FoodSearchDto> foods)
    {
        var normalizedQuery = NormalizeForSearch(query);
        var searchTerms = normalizedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return foods
            .Select((food, index) => new
            {
                Food = food,
                Index = index,
                Score = CalculateAgentSearchScore(food, normalizedQuery, searchTerms)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Index)
            .Select(item => item.Food)
            .ToList();
    }

    private static int CalculateAgentSearchScore(
        FoodSearchDto food,
        string normalizedQuery,
        IReadOnlyList<string> searchTerms)
    {
        var name = NormalizeForSearch(food.Name);
        var brand = NormalizeForSearch(food.Brand);
        var ean = NormalizeForSearch(food.Ean);
        var description = NormalizeForSearch(food.Description);
        var combinedProduct = NormalizeForSearch($"{food.Brand} {food.Name}");
        var score = 0;

        if (!string.IsNullOrEmpty(normalizedQuery))
        {
            if (ean == normalizedQuery)
                score += 12000;
            if (combinedProduct == normalizedQuery)
                score += 10000;
            if (name == normalizedQuery)
                score += 8500;
            if (combinedProduct.Contains(normalizedQuery))
                score += 1400;
            if (name.Contains(normalizedQuery))
                score += 1200;
            if (brand.Contains(normalizedQuery))
                score += 700;
            if (name.StartsWith(normalizedQuery, StringComparison.Ordinal))
                score += 350;
            if (brand.StartsWith(normalizedQuery, StringComparison.Ordinal))
                score += 250;
        }

        foreach (var searchTerm in searchTerms)
        {
            if (name == searchTerm)
                score += 500;
            if (brand == searchTerm)
                score += 450;
            if (ean == searchTerm)
                score += 900;
            if (name.Contains(searchTerm))
                score += 100;
            if (brand.Contains(searchTerm))
                score += 80;
            if (description.Contains(searchTerm))
                score += 25;
            if (food.Tags.Any(tag => NormalizeForSearch(tag).Contains(searchTerm)))
                score += 25;
        }

        if (food.PreviouslyEaten)
            score += 300;

        return score;
    }

    private static string NormalizeForSearch(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

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
