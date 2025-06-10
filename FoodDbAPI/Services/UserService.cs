using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Services.Interfaces;

namespace FoodDbAPI.Services;

public class UserService(FoodDbContext context, ILogger<UserService> logger) : IUserService
{
    public async Task<UserProfileDto> GetProfileAsync(int userId)
    {
        var user = await context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new ArgumentException("User not found");
        }

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CurrentWeight = user.CurrentWeight,
            CreatedAt = user.CreatedAt,
            Role = user.Role
        };
    }

    public async Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateUserProfileRequest request)
    {
        var user = await context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new ArgumentException("User not found");
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.CurrentWeight = request.CurrentWeight;
        user.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogInformation("User profile updated: {UserId}", userId);

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CurrentWeight = user.CurrentWeight,
            CreatedAt = user.CreatedAt,
            Role = user.Role
        };
    }
}