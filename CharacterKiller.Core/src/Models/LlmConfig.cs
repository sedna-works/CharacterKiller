namespace CharacterKiller.Core.Models;

/// <summary>
/// LLM 服务配置。
/// </summary>
public class LlmConfig
{
    public string Provider { get; set; } = "openai";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = "gpt-4o-mini";

    public string ApiKey { get; set; } = string.Empty;

    public int MaxRetries { get; set; } = 3;

    public int TimeoutSeconds { get; set; } = 120;
}
