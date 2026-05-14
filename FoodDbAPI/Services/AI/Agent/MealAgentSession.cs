using FoodDbAPI.DTOs;
using FoodDbAPI.Services.AI.Abstractions;

namespace FoodDbAPI.Services.AI.Agent;

public class MealAgentSession
{
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public int UserId { get; init; }
    public List<AIMessage> Messages { get; set; } = new();
    public List<MealDraftItem> MealDraft { get; set; } = new();

    /// <summary>
    /// Food lookup cache populated by search_food and used by suggest_food + update_meal_draft
    /// to avoid redundant DB round-trips within the same session.
    /// </summary>
    public Dictionary<int, FoodSearchDto> FoodCache { get; set; } = new();

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}
