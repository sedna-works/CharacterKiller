namespace CharacterKiller.Core.Models;

/// <summary>
/// Skills 生成任务特有的 checkpoint 状态。
/// </summary>
public class SkillsTaskState
{
    /// <summary>
    /// 作为输入的 summary 文件路径。
    /// </summary>
    public string SummaryFilePath { get; set; } = string.Empty;

    /// <summary>
    /// 最终输出文件路径。
    /// </summary>
    public string? FinalOutputPath { get; set; }
}
