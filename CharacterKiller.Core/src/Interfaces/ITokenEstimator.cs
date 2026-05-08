namespace CharacterKiller.Core.Interfaces;

/// <summary>
/// Token 估算器抽象。
/// </summary>
public interface ITokenEstimator
{
    int Estimate(string text);
}
