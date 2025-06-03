using FoodDbAPI.DTOs;
using FoodDbAPI.Models;

namespace FoodDbAPI.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<bool> ConfirmEmailAsync(ConfirmEmailRequest req);
    string GenerateJwtToken(int userId, string email, AppRole role);
}