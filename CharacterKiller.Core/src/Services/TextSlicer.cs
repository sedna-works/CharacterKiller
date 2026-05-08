using System.Text;
using CharacterKiller.Core.Interfaces;
using CharacterKiller.Core.Models;

namespace CharacterKiller.Core.Services;

/// <summary>
/// 文本切片器，按 token 估算将长文本切分为段落对齐的块。
/// </summary>
public class TextSlicer
{
    private readonly ITokenEstimator _estimator;

    public TextSlicer(ITokenEstimator estimator)
    {
        _estimator = estimator;
    }

    /// <summary>
    /// 将文本切分为多个块，每块估算 token 数不超过 chunkSizeTokens，
    /// 相邻块之间保留 overlapTokens 的重叠内容以便上下文衔接。
    /// </summary>
    public List<TextChunk> Slice(string text, int chunkSizeTokens, int overlapTokens)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<TextChunk>();
        }

        // 统一换行符后按段落拆分（兼容 \r\n, \n, \r）
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var paragraphs = normalized.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<TextChunk>();
        var currentParagraphs = new List<string>();
        var currentTokens = 0;
        var chunkIndex = 0;

        for (var i = 0; i < paragraphs.Length; i++)
        {
            var para = paragraphs[i].Trim();
            if (string.IsNullOrEmpty(para)) continue;

            var paraTokens = _estimator.Estimate(para);

            // 单段落就超过限制，强制按句切分
            if (paraTokens > chunkSizeTokens && currentParagraphs.Count == 0)
            {
                var sentenceChunks = SplitBySentences(para, chunkSizeTokens);
                foreach (var sc in sentenceChunks)
                {
                    chunks.Add(new TextChunk
                    {
                        Index = chunkIndex++,
                        Content = sc,
                        EstimatedTokens = _estimator.Estimate(sc)
                    });
                }
                continue;
            }

            // 当前累积加上这段会超限，先封包当前块
            if (currentTokens + paraTokens > chunkSizeTokens && currentParagraphs.Count > 0)
            {
                FlushChunk(chunks, currentParagraphs, chunkSizeTokens, overlapTokens, ref chunkIndex);
                currentParagraphs.Clear();
                currentTokens = 0;
            }

            currentParagraphs.Add(para);
            currentTokens += paraTokens;
        }

        // 剩余段落
        if (currentParagraphs.Count > 0)
        {
            FlushChunk(chunks, currentParagraphs, chunkSizeTokens, overlapTokens, ref chunkIndex, isLast: true);
        }

        return chunks;
    }

    private void FlushChunk(
        List<TextChunk> chunks,
        List<string> paragraphs,
        int chunkSizeTokens,
        int overlapTokens,
        ref int chunkIndex,
        bool isLast = false)
    {
        var content = string.Join("\n\n", paragraphs);
        var overlapContent = "";

        // 如果不是最后一块，为下一块准备 overlap 内容
        if (!isLast && overlapTokens > 0)
        {
            overlapContent = ExtractOverlap(paragraphs, overlapTokens);
        }

        chunks.Add(new TextChunk
        {
            Index = chunkIndex++,
            Content = content,
            EstimatedTokens = _estimator.Estimate(content)
        });

        // 将 overlap 内容前置到下一块的起始（通过修改外部列表实现）
        if (!string.IsNullOrEmpty(overlapContent))
        {
            paragraphs.Clear();
            paragraphs.Add(overlapContent);
        }
    }

    private string ExtractOverlap(List<string> paragraphs, int overlapTokens)
    {
        var overlapParagraphs = new List<string>();
        var tokens = 0;

        // 从后往前取段落，直到凑够 overlapTokens
        for (var i = paragraphs.Count - 1; i >= 0; i--)
        {
            var pt = _estimator.Estimate(paragraphs[i]);
            if (tokens + pt > overlapTokens && overlapParagraphs.Count > 0)
                break;

            overlapParagraphs.Insert(0, paragraphs[i]);
            tokens += pt;
        }

        return string.Join("\n\n", overlapParagraphs);
    }

    private List<string> SplitBySentences(string paragraph, int chunkSizeTokens)
    {
        var delimiters = new HashSet<char>(new[] { '。', '？', '！', '.', '?', '!' });
        var result = new List<string>();
        var currentSentences = new List<string>();
        var tokens = 0;
        var sb = new StringBuilder();

        foreach (var c in paragraph)
        {
            sb.Append(c);
            if (delimiters.Contains(c))
            {
                var sentence = sb.ToString();
                var st = _estimator.Estimate(sentence);
                if (tokens + st > chunkSizeTokens && currentSentences.Count > 0)
                {
                    result.Add(string.Join("", currentSentences));
                    currentSentences.Clear();
                    tokens = 0;
                }
                currentSentences.Add(sentence);
                tokens += st;
                sb.Clear();
            }
        }

        // 处理末尾没有标点的残留内容
        if (sb.Length > 0)
        {
            var remainder = sb.ToString();
            var st = _estimator.Estimate(remainder);
            if (tokens + st > chunkSizeTokens && currentSentences.Count > 0)
            {
                result.Add(string.Join("", currentSentences));
                currentSentences.Clear();
                tokens = 0;
            }
            currentSentences.Add(remainder);
        }

        if (currentSentences.Count > 0)
        {
            result.Add(string.Join("", currentSentences));
        }

        return result;
    }
}
