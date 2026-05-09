using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CharacterKiller.Core.Interfaces;
using CharacterKiller.Core.Models;
using CharacterKiller.Core.Resilience;
using Microsoft.Extensions.Logging;

namespace CharacterKiller.Infrastructure.Llm;

/// <summary>
/// 兼容 OpenAI API 格式的 LLM 客户端。
/// 适用于 OpenAI、Kimi、DeepSeek、Ollama（OpenAI 兼容模式）等。
/// 支持流式输出（SSE），可通过 <see cref="IProgress{T}"/> 实时接收生成内容。
/// </summary>
public class OpenAiCompatibleClient : ILlmClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly LlmConfig _config;
    private readonly ILogger<OpenAiCompatibleClient> _logger;
    private readonly SemaphoreSlim _concurrencyLimit;

    public OpenAiCompatibleClient(LlmConfig config, ILogger<OpenAiCompatibleClient> logger)
    {
        _config = config;
        _logger = logger;

        var handler = new SocketsHttpHandler
        {
            // 避免 504 后复用死连接导致无限挂起
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            // 连接建立超时（DNS + TCP 握手）
            ConnectTimeout = TimeSpan.FromSeconds(30),
            // 允许自动解压
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };

        var concurrency = Math.Max(1, config.MaxConcurrency);
        _concurrencyLimit = new SemaphoreSlim(concurrency, concurrency);

        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
        }
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default, IProgress<string>? progress = null)
    {
        await _concurrencyLimit.WaitAsync(ct);
        try
        {
            return await CompleteInternalAsync(systemPrompt, userPrompt, ct, progress);
        }
        finally
        {
            _concurrencyLimit.Release();
        }
    }

    private async Task<string> CompleteInternalAsync(string systemPrompt, string userPrompt, CancellationToken ct, IProgress<string>? progress)
    {
        var request = new ChatCompletionRequest
        {
            Model = _config.Model,
            Stream = true,
            Messages =
            [
                new Message { Role = "system", Content = systemPrompt },
                new Message { Role = "user", Content = userPrompt }
            ]
        };

        var url = _config.BaseUrl.TrimEnd('/') + "/chat/completions";

        return await RetryPolicy.ExecuteAsync(
            async () =>
            {
                using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

                _logger.LogDebug("LLM 请求体大小: ~{Size} chars", systemPrompt.Length + userPrompt.Length);

                var content = JsonContent.Create(request);
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

                var response = await _httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    requestCts.Token);

                _logger.LogInformation("LLM 已响应，状态码: {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("LLM API 返回错误: {StatusCode} - {Body}", response.StatusCode, errorBody);
                    throw new HttpRequestException($"LLM API 错误: {(int)response.StatusCode} - {errorBody}");
                }

                var fullContent = await ReadStreamAsync(response, _logger, requestCts.Token, progress);

                // 当未提供外部 progress 时，保持向后兼容的默认控制台换行
                if (progress == null)
                {
                    Console.WriteLine();
                    Console.Out.Flush();
                }

                if (string.IsNullOrWhiteSpace(fullContent))
                {
                    throw new InvalidOperationException("LLM 返回空内容");
                }

                return fullContent;
            },
            maxRetries: _config.MaxRetries,
            canRetry: ex => ex is not OperationCanceledException || !ct.IsCancellationRequested,
            logger: _logger,
            ct: ct);
    }

    /// <summary>
    /// 读取 SSE 流式响应。
    /// 若提供了 <paramref name="progress"/>，token 会通过其上报，不再直接写控制台；
    /// 若未提供，保持向后兼容的默认控制台输出行为（含 spinner）。
    /// </summary>
    private static async Task<string> ReadStreamAsync(HttpResponseMessage response, ILogger logger, CancellationToken ct, IProgress<string>? progress)
    {
        var sb = new StringBuilder();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        // 启动等待第一个 token 的进度动画（仅在无外部 progress 时内部显示）
        Task? spinnerTask = null;
        CancellationTokenSource? spinnerCts = null;
        var useConsole = progress == null;

        if (useConsole)
        {
            spinnerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            spinnerTask = RunSpinnerAsync(spinnerCts.Token);
        }

        var firstTokenReceived = false;

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null)
            {
                break;
            }
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // SSE 格式: data: {...}
            if (!line.StartsWith("data: "))
            {
                continue;
            }

            var data = line["data: ".Length..];
            if (data == "[DONE]")
            {
                break;
            }

            try
            {
                var chunk = JsonSerializer.Deserialize<ChatCompletionStreamChunk>(data);
                var delta = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
                if (!string.IsNullOrEmpty(delta))
                {
                    if (!firstTokenReceived)
                    {
                        firstTokenReceived = true;
                        if (spinnerCts != null)
                        {
                            spinnerCts.Cancel();
                            if (spinnerTask != null)
                            {
                                try { await spinnerTask; } catch { }
                            }
                            Console.Write("\r                      \r"); // 清除进度行
                        }
                    }

                    sb.Append(delta);

                    if (useConsole)
                    {
                        Console.Write(delta);
                    }
                    else
                    {
                        progress!.Report(delta);
                    }
                }
            }
            catch (JsonException ex)
            {
                // 跳过无法解析的行（如空数据或格式异常），但记录以便排查
                logger.LogDebug("SSE 流中跳过无法解析的行: {Line}, 原因: {Reason}", data, ex.Message);
            }
        }

        // 如果流结束但从未收到 token，取消 spinner
        if (!firstTokenReceived && spinnerCts != null)
        {
            spinnerCts.Cancel();
            if (spinnerTask != null)
            {
                try { await spinnerTask; } catch { }
            }
            Console.Write("\r                      \r");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 在等待第一个 SSE token 时显示旋转进度条。
    /// </summary>
    private static async Task RunSpinnerAsync(CancellationToken ct)
    {
        var chars = new[] { '|', '/', '-', '\\' };
        int i = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                Console.Write($"\r等待模型首 token... {chars[i++ % chars.Length]}");
                await Task.Delay(200, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        _concurrencyLimit.Dispose();
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

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    private class Message
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    // 非流式响应 DTO（保留以备 fallback 需要）
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

    // 流式响应 DTO
    private class ChatCompletionStreamChunk
    {
        [JsonPropertyName("choices")]
        public List<StreamChoice>? Choices { get; set; }
    }

    private class StreamChoice
    {
        [JsonPropertyName("delta")]
        public StreamDelta? Delta { get; set; }
    }

    private class StreamDelta
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
