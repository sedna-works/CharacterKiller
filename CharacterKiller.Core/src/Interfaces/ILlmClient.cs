namespace CharacterKiller.Core.Interfaces;

/// <summary>
/// LLM 客户端抽象。
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// 单轮对话补全。
    /// 流式模式下会实时输出到控制台。
    /// </summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
