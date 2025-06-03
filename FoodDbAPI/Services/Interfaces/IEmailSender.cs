namespace FoodDbAPI.Services.Interfaces;

public interface IEmailSender
{
    Task SendEmailAsync(string to, string subject, string htmlMessage);
}