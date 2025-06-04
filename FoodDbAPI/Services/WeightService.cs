using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Models;
using FoodDbAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FoodDbAPI.Services;

public class WeightService(FoodDbContext context, ILogger<WeightService> logger) : IWeightService
{
    public async Task<WeightEntryDto> AddWeightEntryAsync(int userId, CreateWeightEntryRequest request)
    {
        var weightEntry = new UserWeightEntry
        {
            UserId = userId,
            Weight = request.Weight,
            RecordedAt = request.RecordedAt.ToUniversalTime(),
            CreatedAt = DateTime.UtcNow
        };

        context.WeightEntries.Add(weightEntry);
        await context.SaveChangesAsync();

        // Update user's current weight
        var user = await context.Users.FindAsync(userId);
        if (user != null)
        {
            user.CurrentWeight = request.Weight;
            user.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        logger.LogInformation("Weight entry added for user {UserId}: {Weight}kg", userId, request.Weight);

        return new WeightEntryDto
        {
            Id = weightEntry.Id,
            Weight = weightEntry.Weight,
            RecordedAt = weightEntry.RecordedAt,
            CreatedAt = weightEntry.CreatedAt
        };
    }

    public async Task<List<WeightEntryDto>> GetWeightHistoryAsync(int userId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = context.WeightEntries.Where(w => w.UserId == userId);

        if (startDate.HasValue)
            query = query.Where(w => w.RecordedAt >= startDate.Value.ToUniversalTime());

        if (endDate.HasValue)
            query = query.Where(w => w.RecordedAt <= endDate.Value.ToUniversalTime());

        var entries = await query
            .OrderByDescending(w => w.RecordedAt)
            .Select(w => new WeightEntryDto
            {
                Id = w.Id,
                Weight = w.Weight,
                RecordedAt = w.RecordedAt,
                CreatedAt = w.CreatedAt
            })
            .ToListAsync();

        return entries;
    }

    public async Task DeleteWeightEntryAsync(int userId, int entryId)
    {
        var entry = await context.WeightEntries
            .FirstOrDefaultAsync(w => w.Id == entryId && w.UserId == userId);

        if (entry == null)
        {
            throw new ArgumentException("Weight entry not found");
        }

        context.WeightEntries.Remove(entry);
        await context.SaveChangesAsync();

        logger.LogInformation("Weight entry deleted: {EntryId} for user {UserId}", entryId, userId);
    }
}