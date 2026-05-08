namespace CharacterKiller.Core.Models;

/// <summary>
/// 批量任务单元配置，用于一次处理多个角色。
/// </summary>
public class JobConfig
{
    /// <summary>
    /// 输入剧本文件列表，按顺序拼接后整体分析。
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
