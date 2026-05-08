using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CharacterKiller.Core.Interfaces;
using CharacterKiller.Core.Models;
using Microsoft.Extensions.Logging;

namespace CharacterKiller.Infrastructure.Llm;

/// <summary>
/// 兼容 OpenAI API 格式的 LLM 客户端。
/// 适用于 OpenAI、Kimi、DeepSeek、Ollama（OpenAI 兼容模式）等。
/// </summary>
public class OpenAiCompatibleClient : ILlmClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly LlmConfig _config;
    private readonly ILogger<OpenAiCompatibleClient> _logger;

    public OpenAiCompatibleClient(LlmConfig config, ILogger<OpenAiCompatibleClient> logger)
    {
        _config = config;
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };

        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
        }
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var request = new ChatCompletionRequest
        {
            Model = _config.Model,
            Messages =
            [
                new Message { Role = "system", Content = systemPrompt },
                new Message { Role = "user", Content = userPrompt }
            ]
        };

        var url = _config.BaseUrl.TrimEnd('/') + "/chat/completions";
        var lastException = default(Exception);

        for (var attempt = 0; attempt <= _config.MaxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    _logger.LogWarning("LLM 请求失败，第 {Attempt} 次重试，等待 {Delay}s...", attempt, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                }

                var response = await _httpClient.PostAsJsonAsync(url, request, ct);
                var responseJson = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("LLM API 返回错误: {StatusCode} - {Body}", response.StatusCode, responseJson);
                    throw new HttpRequestException($"LLM API 错误: {(int)response.StatusCode} - {responseJson}");
                }

                var result = JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson);
                var content = result?.Choices?.FirstOrDefault()?.Message?.Content;

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new InvalidOperationException("LLM 返回空内容");
                }

                return content;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
            }
        }

        throw new InvalidOperationException($"LLM 请求在 {_config.MaxRetries} 次重试后仍然失败", lastException);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    // --- 内部 DTO ---

    private class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<Message> Messages { get; set; } = new();
    }

    private class Message
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }
}
