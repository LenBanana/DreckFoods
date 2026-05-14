namespace FoodDbAPI.Models.Settings;

public class AISettings
{
    /// <summary>Which provider implementation to use. Currently only "OpenAI" is supported.</summary>
    public string Provider { get; set; } = "OpenAI";
    public OpenAIProviderSettings OpenAI { get; set; } = new();
}

public class OpenAIProviderSettings
{
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model name passed directly to the API, e.g. "gpt-5-mini", "gpt-4o".</summary>
    public string Model { get; set; } = "gpt-5-mini";

    public int MaxTokens { get; set; } = 4096;

    /// <summary>Sampling temperature in [0, 2]. Lower = more deterministic.</summary>
    public float Temperature { get; set; } = 0.7f;
}
