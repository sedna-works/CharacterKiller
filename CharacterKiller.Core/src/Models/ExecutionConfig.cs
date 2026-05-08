namespace CharacterKiller.Core.Models;

/// <summary>
/// 执行并发度配置。
/// 默认值为 1，表示顺序执行，保持向后兼容。
/// </summary>
public class ExecutionConfig
{
    /// <summary>
    /// Summarize 阶段单角色的最大切片并发数。
    /// 设为 1 表示顺序执行（默认）。
    /// </summary>
    public int MaxChunkConcurrency { get; set; } = 1;

    /// <summary>
    /// 批量任务（Jobs）的最大并行角色数。
    /// 设为 1 表示顺序执行（默认）。
    /// </summary>
    public int MaxJobConcurrency { get; set; } = 1;
}
