using CharacterKiller.Core.Interfaces;
using CharacterKiller.Core.Models;
using Microsoft.Extensions.Logging;

namespace CharacterKiller.Infrastructure.Llm;

/// <summary>
/// LLM 客户端工厂。
/// 当前 OpenAI、Kimi、DeepSeek、Ollama 等均兼容 OpenAI API 格式，
/// 因此统一返回 OpenAiCompatibleClient。后续若有不兼容的 provider 可在此扩展。
/// </summary>
public static class LlmClientFactory
{
    public static ILlmClient Create(LlmConfig config, ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger<OpenAiCompatibleClient>();

        return config.Provider.ToLowerInvariant() switch
        {
            "openai" => new OpenAiCompatibleClient(config, logger),
            "kimi" => new OpenAiCompatibleClient(config, logger),
            "moonshot" => new OpenAiCompatibleClient(config, logger),
            "deepseek" => new OpenAiCompatibleClient(config, logger),
            "ollama" => new OpenAiCompatibleClient(config, logger),
            _ => throw new NotSupportedException($"不支持的 LLM Provider: {config.Provider}")
        };
    }
}
