namespace FoodDbAPI.Models.Settings;

public class FrontendSettings
{
    public string BaseUrl { get; set; } = string.Empty;
}

public class SmtpSettings
{
    public string Host        { get; set; } = string.Empty;
    public int    Port        { get; set; }
    public string Username    { get; set; } = string.Empty;
    public string Password    { get; set; } = string.Empty;
    public string SenderName  { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
}