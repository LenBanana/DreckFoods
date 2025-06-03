using FoodDbAPI.DTOs;

namespace FoodDbAPI.Services.Interfaces;

public interface IUserService
{
    Task<UserProfileDto> GetProfileAsync(int userId);
    Task<UserProfileDto> UpdateProfileAsync(int userId, UpdateUserProfileRequest request);
}