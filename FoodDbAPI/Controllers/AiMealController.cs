using System.Text.Json;
using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Extensions;
using FoodDbAPI.Services.AI.Agent;
using FoodDbAPI.Services.AI.Sessions;
using FoodDbAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace FoodDbAPI.Controllers;

[ApiController]
[Route("api/ai-meal")]
[Authorize(Roles = AppRoles.Admin)]
public class AiMealController(
    IMealAgent mealAgent,
    IMealSessionStore sessionStore,
    IMealService mealService,
    IFoodEntryService foodEntryService,
    ILogger<AiMealController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions SseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Session management ────────────────────────────────────────────────────

    [HttpPost("session")]
    public IActionResult CreateSession([FromBody] StartSessionRequestDto? request)
    {
        var mode = request?.Mode?.ToLowerInvariant() switch
        {
            "plan" => "plan",
            _      => "log"   // default / unknown values fall back to log
        };
        var session = sessionStore.CreateSession(User.GetUserId(), mode);
        return Ok(new StartSessionResponseDto { SessionId = session.SessionId.ToString() });
    }

    [HttpDelete("session/{id:guid}")]
    public IActionResult DeleteSession(Guid id)
    {
        sessionStore.DeleteSession(id);
        return NoContent();
    }

    // ── Draft inspection ──────────────────────────────────────────────────────

    [HttpGet("session/{id:guid}/draft")]
    public IActionResult GetDraft(Guid id)
    {
        var session = sessionStore.GetSession(id);
        if (session == null || session.UserId != User.GetUserId())
            return NotFound();

        var dto = new MealDraftDto
        {
            Items = session.MealDraft.Select(MapToDraftItemDto).ToList()
        };
        return Ok(dto);
    }

    // ── SSE streaming ─────────────────────────────────────────────────────────

    [HttpPost("session/{id:guid}/message")]
    public async Task StreamMessage(
        Guid id,
        [FromBody] SendMessageRequestDto request,
        CancellationToken ct)
    {
        var session = sessionStore.GetSession(id);
        if (session == null || session.UserId != User.GetUserId())
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Disable buffering so chunks are flushed to the client immediately.
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        Response.Headers["Content-Type"]      = "text/event-stream";
        Response.Headers["Cache-Control"]     = "no-cache";
        Response.Headers["Connection"]        = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";  // nginx: disable proxy buffering

        try
        {
            await foreach (var agentEvent in mealAgent.ChatAsync(id, request.Message, ct))
            {
                var json = SerializeAgentEvent(agentEvent);
                await Response.WriteAsync($"data: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — no further writes needed.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI Meal stream error for session {SessionId}", id);

            // Best-effort: forward the error to the client before closing the stream.
            try
            {
                var errorJson = JsonSerializer.Serialize(
                    new { type = "error", data = new { message = ex.Message } },
                    SseOptions);
                await Response.WriteAsync($"data: {errorJson}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
            catch
            {
                // Ignore — stream may already be broken.
            }
        }
    }

    // ── Confirm flow ──────────────────────────────────────────────────────────

    [HttpPost("session/{id:guid}/confirm")]
    public async Task<IActionResult> ConfirmMeal(
        Guid id,
        [FromBody] ConfirmMealRequestDto request)
    {
        var session = sessionStore.GetSession(id);
        if (session == null || session.UserId != User.GetUserId())
            return NotFound();

        if (session.MealDraft.Count == 0)
            return BadRequest(new { message = "Meal draft is empty." });

        var userId     = User.GetUserId();
        var consumedAt = request.ConsumedAt ?? DateTime.UtcNow;
        var response   = new ConfirmMealResponseDto();

        // ── Plan mode: save as meal template only, no logging ─────────────────
        if (!request.LogNow)
        {
            var createDto = new CreateMealDto
            {
                Name  = request.Name,
                Items = session.MealDraft
                    .Select(i => new MealItemDto { FddbFoodId = i.FddbFoodId, Weight = i.WeightGrams })
                    .ToList()
            };
            response.Meal = await mealService.CreateMealAsync(userId, createDto);
            sessionStore.DeleteSession(id);
            return Ok(response);
        }

        // ── Log mode: log entries (optionally also create meal template) ──────
        if (request.SaveAsMeal)
        {
            // Create a reusable meal template from the draft items.
            var createDto = new CreateMealDto
            {
                Name  = request.Name,
                Items = session.MealDraft
                    .Select(i => new MealItemDto { FddbFoodId = i.FddbFoodId, Weight = i.WeightGrams })
                    .ToList()
            };
            var meal = await mealService.CreateMealAsync(userId, createDto);

            // Log the full portion — portionWeight == totalWeight preserves each item's exact weight.
            var entries = await mealService.AddMealPortionAsync(userId, new AddMealPortionDto
            {
                MealId     = meal.Id,
                Weight     = meal.TotalWeight,
                ConsumedAt = consumedAt
            });

            response.Meal    = meal;
            response.Entries = entries;
        }
        else
        {
            // Log food entries directly without creating a reusable meal record.
            foreach (var item in session.MealDraft)
            {
                var entry = await foodEntryService.AddFoodEntryAsync(userId, new CreateFoodEntryRequest
                {
                    FddbFoodId    = item.FddbFoodId,
                    GramsConsumed = item.WeightGrams,
                    ConsumedAt    = consumedAt
                });
                response.Entries.Add(entry);
            }
        }

        sessionStore.DeleteSession(id);
        return Ok(response);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MealDraftItemDto MapToDraftItemDto(MealDraftItem item) => new()
    {
        FddbFoodId  = item.FddbFoodId,
        FoodName    = item.FoodName,
        WeightGrams = item.WeightGrams,
        ImageUrl    = item.ImageUrl,
        Nutrition   = item.Nutrition
    };

    /// <summary>
    /// Serialises an <see cref="AgentEvent"/> to an SSE-ready JSON string.
    /// Each branch creates a fully-typed anonymous object so System.Text.Json
    /// emits correct camelCase output without losing property information.
    /// </summary>
    private string SerializeAgentEvent(AgentEvent ev) => ev switch
    {
        AgentTextChunk e => JsonSerializer.Serialize(
            new { type = "text_chunk", data = new { text = e.Text } },
            SseOptions),

        AgentToolSearching e => JsonSerializer.Serialize(
            new { type = "tool_searching", data = new { query = e.Query } },
            SseOptions),

        AgentFoodResults e => JsonSerializer.Serialize(
            new { type = "food_results", data = new { query = e.Query, foods = e.Foods } },
            SseOptions),

        AgentMealDraftUpdated e => JsonSerializer.Serialize(
            new { type = "meal_draft_updated", data = new { items = e.Items.Select(MapToDraftItemDto).ToList() } },
            SseOptions),

        AgentAssistantMessage e => JsonSerializer.Serialize(
            new { type = "assistant_message", data = new { text = e.Text } },
            SseOptions),

        AgentDone => JsonSerializer.Serialize(
            new { type = "done", data = new { } },
            SseOptions),

        AgentError e => JsonSerializer.Serialize(
            new { type = "error", data = new { message = e.Message } },
            SseOptions),

        AgentSuggestFood e => JsonSerializer.Serialize(
            new { type = "suggest_food", data = new { suggestions = e.Suggestions } },
            SseOptions),

        AgentAskQuestions e => JsonSerializer.Serialize(
            new { type = "ask_questions", data = new { questions = e.Questions } },
            SseOptions),

        _ => JsonSerializer.Serialize(
            new { type = "unknown", data = new { } },
            SseOptions)
    };
}
