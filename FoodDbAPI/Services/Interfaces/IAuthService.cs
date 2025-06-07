using FoodDbAPI.DTOs;
using FoodDbAPI.Models;
using Microsoft.AspNetCore.Identity.Data;
using LoginRequest = FoodDbAPI.DTOs.LoginRequest;
using RegisterRequest = FoodDbAPI.DTOs.RegisterRequest;

namespace FoodDbAPI.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<bool> DeleteUserAsync(int userId);
    Task<bool> ConfirmEmailAsync(ConfirmEmailRequest req);
    Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request);
    Task<bool> ForgotPasswordAsync(ForgotPasswordRequest req);
    Task<bool> ResetPasswordAsync(ResetPasswordRequest req);
    string GenerateJwtToken(int userId, string email, AppRole role);
}