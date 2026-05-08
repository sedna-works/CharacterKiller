using CharacterKiller.Core.Interfaces;

namespace CharacterKiller.Core.Services;

/// <summary>
/// 基于 token 限制的内容截断工具。
/// </summary>
public class TokenLimiter
{
    private readonly ITokenEstimator _estimator;

    public TokenLimiter(ITokenEstimator estimator)
    {
        _estimator = estimator;
    }

    /// <summary>
    /// 截断文本至指定 token 数以内，优先保留头部内容。
    /// </summary>
    public string TruncateToTokens(string text, int maxTokens)
    {
        if (_estimator.Estimate(text) <= maxTokens)
        {
            return text;
        }

        // 二分查找截断位置
        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            var candidate = text[..mid];
            if (_estimator.Estimate(candidate) <= maxTokens)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return text[..low];
    }
}
