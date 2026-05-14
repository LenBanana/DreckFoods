using FoodDbAPI.Services.AI.Agent;

namespace FoodDbAPI.Services.AI.Sessions;

public interface IMealSessionStore
{
    MealAgentSession CreateSession(int userId);
    MealAgentSession? GetSession(Guid sessionId);
    void UpdateSession(MealAgentSession session);
    void DeleteSession(Guid sessionId);
}
