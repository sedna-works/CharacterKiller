namespace CharacterKiller.Core.Models;

/// <summary>
/// 文本切片块。
/// </summary>
public class TextChunk
{
    public int Index { get; set; }

    public string Content { get; set; } = string.Empty;

    public int EstimatedTokens { get; set; }
}
