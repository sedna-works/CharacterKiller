namespace CharacterKiller.Core.Models;

/// <summary>
/// 文本切片策略配置。
/// </summary>
public class SlicingConfig
{
    public int ChunkSizeTokens { get; set; } = 50000;

    public int OverlapTokens { get; set; } = 500;
}
