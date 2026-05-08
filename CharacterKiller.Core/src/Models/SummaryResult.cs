namespace CharacterKiller.Core.Models;

/// <summary>
/// 单段或最终归纳结果。
/// </summary>
public class SummaryResult
{
    public string CharacterName { get; set; } = string.Empty;

    public List<string> Segments { get; set; } = new();

    public DateTime GeneratedAt { get; set; }
}
