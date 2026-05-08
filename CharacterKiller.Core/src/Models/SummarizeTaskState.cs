namespace CharacterKiller.Core.Models;

/// <summary>
/// Summarize 任务特有的 checkpoint 状态。
/// </summary>
public class SummarizeTaskState
{
    /// <summary>
    /// 所有文本切片，恢复时用于校验索引一致性。
    /// </summary>
    public List<TextChunk> Chunks { get; set; } = new();

    /// <summary>
    /// 切片索引到临时文件名的映射（大内容分离存储）。
    /// </summary>
    public Dictionary<int, string> SliceOutputFiles { get; set; } = new();

    /// <summary>
    /// 最终汇总输出文件路径。
    /// </summary>
    public string? FinalOutputPath { get; set; }
}
