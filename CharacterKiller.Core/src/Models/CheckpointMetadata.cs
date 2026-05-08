namespace CharacterKiller.Core.Models;

/// <summary>
/// Checkpoint 元数据，与具体任务类型无关。
/// </summary>
public class CheckpointMetadata
{
    public string CheckpointId { get; set; } = string.Empty;

    public string TaskType { get; set; } = string.Empty;

    public CheckpointStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 创建 checkpoint 时的原始请求参数快照，恢复时用于回填配置。
    /// </summary>
    public Dictionary<string, object> InputParams { get; set; } = new();
}
