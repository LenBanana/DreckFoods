using System.Collections.Concurrent;
using FoodDbAPI.Services.AI.Agent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FoodDbAPI.Services.AI.Sessions;

/// <summary>
/// Thread-safe in-memory store for <see cref="MealAgentSession"/> objects.
/// Also acts as a <see cref="BackgroundService"/> that purges sessions older than
/// <see cref="SessionTtl"/> every <see cref="CleanupInterval"/>.
/// </summary>
public sealed class InMemoryMealSessionStore : BackgroundService, IMealSessionStore
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(2);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<Guid, MealAgentSession> _sessions = new();
    private readonly ILogger<InMemoryMealSessionStore> _logger;

    public InMemoryMealSessionStore(ILogger<InMemoryMealSessionStore> logger)
    {
        _logger = logger;
    }

    // ── IMealSessionStore ─────────────────────────────────────────────────────

    public MealAgentSession CreateSession(int userId)
    {
        var session = new MealAgentSession { UserId = userId };
        _sessions[session.SessionId] = session;
        _logger.LogDebug("Created session {SessionId} for user {UserId}", session.SessionId, userId);
        return session;
    }

    public MealAgentSession? GetSession(Guid sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public void UpdateSession(MealAgentSession session)
    {
        session.LastActivityAt = DateTime.UtcNow;
        _sessions[session.SessionId] = session;
    }

    public void DeleteSession(Guid sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        _logger.LogDebug("Deleted session {SessionId}", sessionId);
    }

    // ── BackgroundService ─────────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Meal session cleanup service started.");

        using var timer = new PeriodicTimer(CleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            PurgeExpiredSessions();
        }

        _logger.LogInformation("Meal session cleanup service stopped.");
    }

    private void PurgeExpiredSessions()
    {
        var cutoff = DateTime.UtcNow - SessionTtl;
        var expired = _sessions
            .Where(kv => kv.Value.LastActivityAt < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in expired)
        {
            _sessions.TryRemove(id, out _);
        }

        if (expired.Count > 0)
            _logger.LogInformation("Purged {Count} expired meal session(s).", expired.Count);
    }
}
