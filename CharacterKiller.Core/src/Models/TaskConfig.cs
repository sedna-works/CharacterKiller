namespace CharacterKiller.Core.Models;

/// <summary>
/// 任务运行时配置。
/// </summary>
public class TaskConfig
{
    /// <summary>
    /// 单文件输入（向后兼容）。
    /// 如果 <see cref="InputFiles"/> 为空，则使用此字段。
    /// </summary>
    public string InputFile { get; set; } = string.Empty;

    /// <summary>
    /// 多文件输入列表，按顺序拼接后整体分析。
    /// 若此列表不为空，则优先使用列表，忽略 <see cref="InputFile"/>。
    /// </summary>
    public List<string> InputFiles { get; set; } = new();

    public string CharacterName { get; set; } = string.Empty;

    public string? VndbCharacterId { get; set; }

    public string OutputDirectory { get; set; } = "output";

    /// <summary>
    /// 输出模式。可选值："roleplay"（默认，生成 AI 角色扮演 skill）或 "template"（生成可复用的小说人物模板）。
    /// </summary>
    public string OutputMode { get; set; } = "roleplay";
}
