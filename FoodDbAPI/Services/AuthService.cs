using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Models;
using FoodDbAPI.Models.Settings;
using FoodDbAPI.Services.Interfaces;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using LoginRequest = FoodDbAPI.DTOs.LoginRequest;
using RegisterRequest = FoodDbAPI.DTOs.RegisterRequest;

namespace FoodDbAPI.Services;

public class AuthService(
    FoodDbContext context,
    IConfiguration configuration,
    ILogger<AuthService> logger,
    IEmailSender emailSender,
    IOptions<FrontendSettings> frontendOpt)
    : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (await context.Users.AnyAsync(u => u.Email == request.Email))
            throw new ArgumentException("A user with that email already exists.");

        // create and persist the new user
        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName ?? "",
            LastName = request.LastName ?? "",
            CurrentWeight = request.CurrentWeight,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Role = AppRole.User,
            EmailConfirmed = false
        };

        // generate a one-time token
        var plainToken = Guid.NewGuid().ToString("N");
        user.EmailTokenHash = BCrypt.Net.BCrypt.HashPassword(plainToken);

        context.Users.Add(user);
        await context.SaveChangesAsync();
        logger.LogInformation("New user registered: {Email}", user.Email);

        var link =
            $"{frontendOpt.Value.BaseUrl.TrimEnd('/')}/auth/confirm-email?userId={user.Id}&token={WebUtility.UrlEncode(plainToken)}";
        var html = $$"""
                     
                                         <html>
                                         <head>
                                           <meta charset='utf-8' />
                                           <style>
                                             body { font-family: Arial, sans-serif; background:#f4f4f4; }
                                             .container { max-width:600px; margin:30px auto; background:#fff; padding:20px; border-radius:8px; }
                                             .header { font-size:22px; font-weight:bold; color:#333; }
                                             .btn { display:inline-block; margin-top:20px; padding:12px 20px;
                                                     background:#28a745; color:#fff; text-decoration:none; border-radius:4px; }
                                             .footer { margin-top:30px; font-size:12px; color:#888; }
                                           </style>
                                         </head>
                                         <body>
                                           <div class='container'>
                                             <div class='header'>Confirm your email</div>
                                             <p>Hi {{WebUtility.HtmlEncode(user.FirstName)}},</p>
                                             <p>Thank you for registering at DreckFoods. Please confirm your email address by clicking the button below:</p>
                                             <a href='{{link}}' class='btn'>Confirm Email</a>
                                             <p>If that doesn’t work, copy & paste this link into your browser:</p>
                                             <p><a href='{{link}}'>{{link}}</a></p>
                                             <div class='footer'>
                                               <p>If you didn’t sign up, just ignore this email.</p>
                                             </div>
                                           </div>
                                         </body>
                                         </html>
                     """;

        await emailSender.SendEmailAsync(user.Email, "Please confirm your email", html);

        return new AuthResponse
        {
            Token = "",
            User = MapToUserProfileDto(user)
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new ArgumentException("Invalid email or password.");

        if (!user.EmailConfirmed)
            throw new ArgumentException("Please confirm your email before logging in.");

        // generate and return the JWT
        var token = GenerateJwtToken(user.Id, user.Email, user.Role);
        return new AuthResponse { Token = token, User = MapToUserProfileDto(user) };
    }
    
    public async Task<bool> DeleteUserAsync(int userId)
    {
        var user = await context.Users.FindAsync(userId);
        if (user == null)
            throw new UnauthorizedAccessException("User not found.");

        context.Users.Remove(user);
        await context.SaveChangesAsync();

        logger.LogInformation("User deleted: {Email}", user.Email);
        return true;
    }

    public async Task<bool> ConfirmEmailAsync(ConfirmEmailRequest request)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.EmailTokenHash != null);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Token, user.EmailTokenHash))
            return false;

        // update user to confirm email
        user.EmailConfirmed = true;
        user.EmailTokenHash = null;
        await context.SaveChangesAsync();

        logger.LogInformation("Email confirmed for user: {Email}", user.Email);
        return true;
    }
    
    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await context.Users.FindAsync(userId);
        if (user == null)
            throw new UnauthorizedAccessException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            throw new ArgumentException("Old password is incorrect.");

        // update user with new password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        logger.LogInformation("Password changed for user: {Email}", user.Email);
        return true;
    }
    
    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest req)
    {
        var user = await context.Users.SingleOrDefaultAsync(u => u.Email == req.Email);
        if (user is not { EmailConfirmed: true })
            return false;

        // generate a one-time token
        var plainToken = Guid.NewGuid().ToString("N");
        user.EmailTokenHash = BCrypt.Net.BCrypt.HashPassword(plainToken);
        await context.SaveChangesAsync();

        var link =
            $"{frontendOpt.Value.BaseUrl.TrimEnd('/')}/auth/reset-password?userId={user.Id}&token={WebUtility.UrlEncode(plainToken)}";
        var html = $$"""
                     <html>
                     <head>
                       <meta charset='utf-8' />
                       <style>
                         body { font-family: Arial, sans-serif; background:#f4f4f4; }
                         .container { max-width:600px; margin:30px auto; background:#fff; padding:20px; border-radius:8px; }
                         .header { font-size:22px; font-weight:bold; color:#333; }
                         .btn { display:inline-block; margin-top:20px; padding:12px 20px;
                                 background:#007bff; color:#fff; text-decoration:none; border-radius:4px; }
                         .footer { margin-top:30px; font-size:12px; color:#888; }
                       </style>
                     </head>
                     <body>
                       <div class='container'>
                         <div class='header'>Reset your password</div>
                         <p>Hi {{WebUtility.HtmlEncode(user.FirstName)}},</p>
                         <p>We received a request to reset your password. Please click the button below to set a new password:</p>
                         <a href='{{link}}' class='btn'>Reset Password</a>
                         <p>If that doesn’t work, copy & paste this link into your browser:</p>
                         <p><a href='{{link}}'>{{link}}</a></p>
                         <div class='footer'>
                           <p>If you didn’t request this, just ignore this email.</p>
                         </div>
                       </div>
                     </body>
                     </html>
                     """;

        await emailSender.SendEmailAsync(user.Email, "Reset your password", html);
        return true;
    }
    
    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.EmailTokenHash != null);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.ResetCode, user.EmailTokenHash))
            return false;

        // update user with new password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.EmailTokenHash = null;
        await context.SaveChangesAsync();

        logger.LogInformation("Password reset for user: {Email}", user.Email);
        return true;
    }

    public string GenerateJwtToken(int userId, string email, AppRole role)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var key = Encoding.ASCII.GetBytes(jwtSettings["Secret"]!);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()), 
                new Claim("id", userId.ToString()),
                new Claim("email", email),
                new Claim(ClaimTypes.Role, role.ToString())
            ]),
            Expires = DateTime.UtcNow.AddDays(Convert.ToDouble(jwtSettings["ExpirationInDays"])),
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static UserProfileDto MapToUserProfileDto(User user)
    {
        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CurrentWeight = user.CurrentWeight,
            CreatedAt = user.CreatedAt
        };
    }
}