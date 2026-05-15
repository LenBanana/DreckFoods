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
        Description = "Search the food database for a food item by generic name. " +
                      "Use broad ingredient names (e.g. 'Reis', 'Hähnchenfilet', 'Champignons'), never brand names. " +
                      "Returns the top matching foods with nutritional values per 100 g and their database IDs.",
        ParametersJsonSchema = JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "query": {
                  "type": "string",
                  "description": "Generic ingredient name, e.g. \"Reis\", \"Hähnchenfilet\", \"Champignons\""
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
                      "Call this ONCE after all search_food calls are done. Do NOT write any text before or after this call. " +
                      "Always set preselected_food_id to your best candidate for each ingredient. " +
                      "The user reviews pre-selections and confirms — do not call search_food again after this.",
        ParametersJsonSchema = JsonDocument.Parse("""
            {
              "type": "object",
              "properties": {
                "suggestions": {
                  "type": "array",
                  "description": "One entry per ingredient, with 2-4 candidate food IDs ordered by best match.",
                  "items": {
                    "type": "object",
                    "properties": {
                      "ingredient_label": {
                        "type": "string",
                        "description": "Human-readable label shown to the user, e.g. \"Beutelreis (125g)\""
                      },
                      "quantity_grams": {
                        "type": "number",
                        "description": "Confirmed quantity in grams. Use 0 only if still unknown after ask_questions."
                      },
                      "candidate_food_ids": {
                        "type": "array",
                        "description": "2-4 food IDs from search_food results or the previously-eaten list, best match first.",
                        "items": { "type": "integer" }
                      },
                      "preselected_food_id": {
                        "type": "integer",
                        "description": "The food ID you consider the best match. The UI pre-selects this so the user only needs to review. Always provide this when you have a clear best candidate."
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
        Description = "Ask the user one or more clarifying questions (e.g., missing quantities) via a structured widget. " +
                      "Call this BEFORE searching when amounts are unknown. Do NOT write any text before or after this call. " +
                      "The user answers in the UI and the answers arrive in the next message.",
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
                        "description": "The question displayed to the user, e.g. \"Wie viel Sojasauce?\""
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
        Description = "Replace the current meal draft with the confirmed items. " +
                      "Call this immediately after receiving '[Confirmed food selections]' from the user. " +
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
    public static readonly IList<AIToolDefinition> All = [SearchFood, AskQuestions, SuggestFood, UpdateMealDraft];
}
