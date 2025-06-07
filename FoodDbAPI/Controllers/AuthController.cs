using FoodDbAPI.DTOs;
using FoodDbAPI.Extensions;
using FoodDbAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using LoginRequest = FoodDbAPI.DTOs.LoginRequest;
using RegisterRequest = FoodDbAPI.DTOs.RegisterRequest;

namespace FoodDbAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService, ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        try
        {
            var response = await authService.RegisterAsync(request);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Registration failed: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during registration");
            return StatusCode(500, new { message = "An error occurred during registration" });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        try
        {
            var response = await authService.LoginAsync(request);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Login failed: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during login");
            return StatusCode(500, new { message = "An error occurred during login" });
        }
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest req)
    {
        try 
        {
            var result = await authService.ConfirmEmailAsync(req);
            if (result)
            {
                return Ok(new { message = "Email confirmed successfully" });
            }
            return BadRequest(new { message = "Invalid confirmation token or email" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error confirming email");
            return StatusCode(500, new { message = "An error occurred while confirming email" });
        }
    }
    
    
    [HttpPost("delete-account")]
    [Authorize]
    public async Task<IActionResult> DeleteAccount()
    {
        try
        {
            var userId = User.GetUserId();
            var result = await authService.DeleteUserAsync(userId);
            if (result)
            {
                return Ok(new { message = "Account deleted successfully" });
            }
            return BadRequest(new { message = "Failed to delete account" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting account");
            return StatusCode(500, new { message = "An error occurred while deleting account" });
        }
    }
    
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        try
        {
            var userId = User.GetUserId();
            var result = await authService.ChangePasswordAsync(userId, req);
            if (result)
            {
                return Ok(new { message = "Password changed successfully" });
            }
            return BadRequest(new { message = "Invalid current password or new password" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error changing password");
            return StatusCode(500, new { message = "An error occurred while changing password" });
        }
    }
    
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req)
    {
        try
        {
            var result = await authService.ForgotPasswordAsync(req);
            if (result)
            {
                return Ok(new { message = "Password reset link sent to your email" });
            }
            return BadRequest(new { message = "Email not found or invalid request" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing forgot password request");
            return StatusCode(500, new { message = "An error occurred while processing forgot password" });
        }
    }
    
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest req)
    {
        try
        {
            var result = await authService.ResetPasswordAsync(req);
            if (result)
            {
                return Ok(new { message = "Password reset successfully" });
            }
            return BadRequest(new { message = "Invalid reset token or password" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resetting password");
            return StatusCode(500, new { message = "An error occurred while resetting password" });
        }
    }
}