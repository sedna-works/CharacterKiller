namespace CharacterKiller.Core.Models;

/// <summary>
/// Checkpoint 列表项摘要，用于展示和管理。
/// </summary>
public class CheckpointSummary
{
    public string CheckpointId { get; set; } = string.Empty;

    public string TaskType { get; set; } = string.Empty;

    public CheckpointStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
