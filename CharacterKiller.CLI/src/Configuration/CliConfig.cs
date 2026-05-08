using CharacterKiller.Core.Models;

namespace CharacterKiller.CLI.Configuration;

/// <summary>
/// CLI 配置根节点，与 appsettings.json 结构对应。
/// </summary>
public class CliConfig
{
    public LlmConfig Llm { get; set; } = new();

    /// <summary>
    /// 单任务配置（命令行模式使用）。
    /// </summary>
    public TaskConfig Task { get; set; } = new();

    /// <summary>
    /// 批量任务列表（配置文件批量模式使用）。
    /// 若此列表不为空，则优先执行批量任务，忽略 <see cref="Task"/>。
    /// </summary>
    public List<JobConfig> Jobs { get; set; } = new();

    public SlicingConfig Slicing { get; set; } = new();

    public CheckpointConfig Checkpoint { get; set; } = new();
}
