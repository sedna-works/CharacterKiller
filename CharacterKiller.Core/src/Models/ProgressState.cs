namespace CharacterKiller.Core.Models;

/// <summary>
/// 通用进度状态，适用于任何可分步执行的任务。
/// </summary>
public class ProgressState
{
    public int CurrentStep { get; set; }

    public int TotalSteps { get; set; }

    public string CurrentPhase { get; set; } = string.Empty;

    public List<int> CompletedItems { get; set; } = new();

    public List<int> FailedItems { get; set; } = new();

    public List<int> PendingItems { get; set; } = new();
}
