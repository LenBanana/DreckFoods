using System.Text.Json;
using FoodDbAPI.Services.AI.Abstractions;

namespace FoodDbAPI.Services.AI.Agent;

/// <summary>
/// Static registry of all tool definitions exposed to the AI model.
/// Schemas are built once at class initialisation and reused across all sessions.
/// </summary>
public static class MealAgentTools
{
    public static readonly AIToolDefinition SearchFood = new()
    {
        Name = "search_food",
        Description = "Search the food database for matching foods. " +
                      "Use exact brand, product, barcode/EAN, flavor, or preparation terms when the user provides them; use generic ingredient names only for generic ingredients or fallback searches. " +
                      "The database matches query words against food name, brand, EAN, and description. If results are weak, retry with fewer/different distinctive words; if too broad, add brand/product/preparation detail. " +
                      "Returns ranked matches with database IDs, brand, nutrition per 100 g, serving hints, and whether the user ate them before.",
        ParametersJsonSchema = JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "query": {
                  "type": "string",
                  "description": "Food search query. Include brand/product/barcode when known, e.g. \"REWE Beste Wahl Basmati Reis\", \"Hähnchenfilet\", \"Champignons\"."
                }
              },
              "required": ["query"]
            }
            """).RootElement.Clone()
    };

    public static readonly AIToolDefinition SuggestFood = new()
    {
        Name = "suggest_food",
        Description = "Present the best food candidates for ALL ingredients at once via a structured selection UI. " +
                "Call this once after necessary searches/questions are done and at least one ingredient still needs user review. Do NOT write any text before or after this call. " +
                "Include exact brand/product matches when available, order candidates best-first, and always set preselected_food_id when one candidate is most likely. " +
                "The user reviews pre-selections and confirms; do not call search_food again after this turn-ending tool.",
        ParametersJsonSchema = JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "suggestions": {
                  "type": "array",
                  "description": "One entry per ingredient needing review, with 1-4 candidate food IDs ordered by best match.",
                  "items": {
                    "type": "object",
                    "properties": {
                      "ingredient_label": {
                        "type": "string",
                        "description": "Human-readable label shown to the user, including brand/product or serving detail when relevant, e.g. \"REWE Basmati Reis (125g)\""
                      },
                      "quantity_grams": {
                        "type": "number",
                        "description": "Best known quantity in grams. Use serving_hints, user history, or confirmed answers; use 0 only if still unknown after asking."
                      },
                      "candidate_food_ids": {
                        "type": "array",
                        "description": "1-4 food IDs from search_food results or the previously-eaten list, best match first.",
                        "items": { "type": "integer" }
                      },
                      "preselected_food_id": {
                        "type": "integer",
                        "description": "The food ID you consider the best match. The UI pre-selects this so the user only needs to review. Always provide this for exact brand/product matches, previously eaten matches, or any clear best candidate."
                      },
                      "allow_custom": {
                        "type": "boolean",
                        "description": "Set true to let the user type a food name if none of the candidates fit."
                      }
                    },
                    "required": ["ingredient_label", "quantity_grams", "candidate_food_ids"]
                  }
                }
              },
              "required": ["suggestions"]
            }
            """).RootElement.Clone()
    };

    public static readonly AIToolDefinition AskQuestions = new()
    {
        Name = "ask_questions",
        Description = "Ask one or more clarifying questions via a structured widget when missing details materially affect food identity or grams. " +
                "Ask about quantities, serving counts, brand/product, cooked vs raw state, or key recipe choices; provide realistic presets and mark your best estimate with recommended:true. " +
                "Do NOT write any text before or after this turn-ending tool. The user's answers arrive in the next message.",
        ParametersJsonSchema = JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "questions": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "id": {
                        "type": "string",
                        "description": "Unique key for this question used to match answers, e.g. \"soy_sauce_amount\""
                      },
                      "label": {
                        "type": "string",
                        "description": "The question displayed to the user, e.g. \"Welche Menge Reis war es?\" or \"Welche Marke war der Joghurt?\""
                      },
                      "choices": {
                        "type": "array",
                        "description": "Preset clickable answers. Include gram equivalents, e.g. \"1 EL (15g)\".",
                        "items": {
                          "type": "object",
                          "properties": {
                            "label": { "type": "string", "description": "Button text shown to user" },
                            "value": { "type": "string", "description": "Value sent back in the answer, e.g. \"15g\"" },
                            "recommended": { "type": "boolean", "description": "Set true on the single choice you consider the best estimate. The UI pre-selects it for the user." }
                          },
                          "required": ["label", "value"]
                        }
                      },
                      "allow_custom": {
                        "type": "boolean",
                        "description": "Whether the user can type a custom answer (defaults to true)"
                      }
                    },
                    "required": ["id", "label"]
                  }
                }
              },
              "required": ["questions"]
            }
            """).RootElement.Clone()
    };

    public static readonly AIToolDefinition UpdateMealDraft = new()
    {
        Name = "update_meal_draft",
        Description = "Replace the current meal draft with all confirmed or certain items. " +
                "Call this immediately after receiving '[Confirmed food selections]' from the user, or directly when exact food IDs and gram quantities are already certain and no useful review step remains. " +
                      "Always include ALL items in the draft, not just new ones.",
        ParametersJsonSchema = JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "items": {
                  "type": "array",
                  "description": "All confirmed food items for the meal.",
                  "items": {
                    "type": "object",
                    "properties": {
                      "food_id":      { "type": "integer", "description": "The food_id from search_food results or previously-eaten list" },
                      "food_name":    { "type": "string",  "description": "Display name of the food" },
                      "weight_grams": { "type": "number",  "description": "Quantity consumed in grams" }
                    },
                    "required": ["food_id", "food_name", "weight_grams"]
                  }
                }
              },
              "required": ["items"]
            }
            """).RootElement.Clone()
    };

    /// <summary>All tools in their recommended invocation order.</summary>
    public static readonly IList<AIToolDefinition> All = [AskQuestions, SearchFood, SuggestFood, UpdateMealDraft];
}
