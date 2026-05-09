namespace CharacterKiller.Core.Interfaces;

/// <summary>
/// LLM 客户端抽象。
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// 单轮对话补全。
    /// 若传入 <paramref name="progress"/>，流式 token 会通过其上报，由调用方决定展示方式；
    /// 若未传入，基础设施层将回退到默认控制台输出。
    /// </summary>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default, IProgress<string>? progress = null);
}
