namespace CharacterKiller.Core.Models;

/// <summary>
/// Checkpoint 持久化配置。
/// </summary>
public class CheckpointConfig
{
    public bool Enabled { get; set; } = true;

    public string Directory { get; set; } = "checkpoints";
}
