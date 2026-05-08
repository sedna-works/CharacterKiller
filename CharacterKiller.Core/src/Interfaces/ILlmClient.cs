namespace CharacterKiller.Core.Interfaces;

/// <summary>
/// LLM 客户端抽象。
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// 单轮对话补全。
    /// </summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
