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

    /// <summary>
    /// 同时向 LLM API 发出的最大并发请求数。
    /// 设为 1 表示顺序请求（默认）。
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;
}
