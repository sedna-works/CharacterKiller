namespace CharacterKiller.Core.Models;

/// <summary>
/// 泛型 Checkpoint 状态容器。
/// TTaskState 为具体任务的状态类型，便于后续扩展新任务而无需修改本结构。
/// </summary>
public class CheckpointState<TTaskState> where TTaskState : class, new()
{
    public CheckpointMetadata Metadata { get; set; } = new();

    public ProgressState Progress { get; set; } = new();

    public TTaskState TaskState { get; set; } = new();
}
