namespace CharacterKiller.Core.Models;

/// <summary>
/// Skills 生成任务特有的 checkpoint 状态（Tool Loop 模式）。
/// </summary>
public class SkillsTaskState
{
    /// <summary>
    /// 作为输入的 summary 文件路径。
    /// </summary>
    public string SummaryFilePath { get; set; } = string.Empty;

    /// <summary>
    /// LLM 对话历史，恢复时直接接续上下文。
    /// </summary>
    public List<LlmMessage> ConversationHistory { get; set; } = new();

    /// <summary>
    /// 当前迭代次数。
    /// </summary>
    public int IterationCount { get; set; }

    /// <summary>
    /// 最终输出文件路径。
    /// </summary>
    public string? FinalOutputPath { get; set; }
}
