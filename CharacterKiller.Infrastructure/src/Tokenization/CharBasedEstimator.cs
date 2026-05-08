using CharacterKiller.Core.Interfaces;

namespace CharacterKiller.Infrastructure.Tokenization;

/// <summary>
/// 基于字符数的 Token 近似估算器。
/// 对中文 Galgame 文本足够用，无需额外依赖。
/// </summary>
public class CharBasedEstimator : ITokenEstimator
{
    public int Estimate(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var chineseChars = 0;
        var englishWords = 0;
        var inEnglishWord = false;

        foreach (var c in text)
        {
            if (char.IsHighSurrogate(c)) continue; // 跳过代理对前半部分，由下一个字符统一处理

            if (c > 127)
            {
                // 中文字符：1 字 ≈ 1 token（实际上通常 1～1.5，保守估算取 1）
                chineseChars++;
                inEnglishWord = false;
            }
            else if (char.IsLetter(c))
            {
                if (!inEnglishWord)
                {
                    englishWords++;
                    inEnglishWord = true;
                }
            }
            else
            {
                inEnglishWord = false;
            }
        }

        // 中文按 1:1，英文单词按 1.3:1
        return chineseChars + (int)(englishWords * 1.3);
    }
}
