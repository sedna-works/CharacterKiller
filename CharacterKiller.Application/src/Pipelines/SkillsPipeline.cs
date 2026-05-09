using System.Text;
using System.Text.Json;
using CharacterKiller.Core.Interfaces;
using CharacterKiller.Core.Models;
using CharacterKiller.Core.Services;
using CharacterKiller.Application.Prompts;
using Microsoft.Extensions.Logging;

namespace CharacterKiller.Application.Pipelines;

/// <summary>
/// Skills 生成流程编排器：读取 summary → 构建 prompt → 调用 LLM → 解析并保存技能包文件夹。
/// </summary>
public class SkillsPipeline
{
    private readonly ILlmClient _llmClient;
    private readonly ICheckpointStore _baseCheckpointStore;
    private readonly ILogger<SkillsPipeline> _logger;
    private readonly CheckpointConfig _checkpointConfig;

    public SkillsPipeline(
        ILlmClient llmClient,
        ICheckpointStore checkpointStore,
        ILogger<SkillsPipeline> logger,
        CheckpointConfig checkpointConfig)
    {
        _llmClient = llmClient;
        _baseCheckpointStore = checkpointStore;
        _logger = logger;
        _checkpointConfig = checkpointConfig;
    }

    public async Task RunAsync(TaskConfig taskConfig, CancellationToken ct = default)
    {
        _logger.LogInformation("开始 Skills 生成流程：角色={Character}", taskConfig.CharacterName);

        // 1. 读取 Summary
        var summaryPath = Path.Combine(taskConfig.OutputDirectory, "summaries", $"{Sanitize(taskConfig.CharacterName)}.md");
        if (!File.Exists(summaryPath))
        {
            throw new FileNotFoundException($"找不到 summary 文件：{summaryPath}。请先执行 summarize 命令。");
        }

        var summaryText = await File.ReadAllTextAsync(summaryPath, ct);

        // 2. 如果 summary 过长，先分片压缩提炼（结果缓存到磁盘，避免重复压缩）
        if (taskConfig.SkillsMaxContextChars > 0 && summaryText.Length > taskConfig.SkillsMaxContextChars)
        {
            var compressedPath = Path.Combine(taskConfig.OutputDirectory, "summaries", $"compressed_{Sanitize(taskConfig.CharacterName)}.md");
            if (File.Exists(compressedPath) && new FileInfo(compressedPath).Length > 0)
            {
                _logger.LogInformation("发现已有压缩 summary，直接读取：{Path}", compressedPath);
                summaryText = await File.ReadAllTextAsync(compressedPath, ct);
            }
            else
            {
                _logger.LogInformation("Summary 长度为 {Length}，超过阈值 {Threshold}，启动分片压缩...", summaryText.Length, taskConfig.SkillsMaxContextChars);
                summaryText = await CompressSummaryAsync(taskConfig.CharacterName, summaryText, taskConfig.SkillsMaxContextChars, ct);
                _logger.LogInformation("压缩后 summary 长度：{Length}", summaryText.Length);
                await AtomicFileWriter.WriteAllTextAsync(compressedPath, summaryText, ct);
                _logger.LogInformation("压缩结果已缓存：{Path}", compressedPath);
            }
        }

        // 3. 计算 Checkpoint ID（任务类型 + 角色名 + 输出模式）
        var checkpointId = $"skills_{Sanitize(taskConfig.CharacterName)}_{taskConfig.OutputMode}";

        // 4. 加载或创建 Checkpoint
        var checkpointDir = Path.Combine(taskConfig.OutputDirectory, _checkpointConfig.Directory);
        var checkpointStore = _baseCheckpointStore.WithBaseDir(checkpointDir);

        var checkpoint = await checkpointStore.LoadAsync<SkillsTaskState>(checkpointId, ct);
        if (checkpoint == null)
        {
            _logger.LogInformation("创建新 Checkpoint：{CheckpointId}", checkpointId);
            checkpoint = new CheckpointState<SkillsTaskState>
            {
                Metadata = new CheckpointMetadata
                {
                    CheckpointId = checkpointId,
                    TaskType = "skills",
                    Status = CheckpointStatus.Running,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    InputParams = new Dictionary<string, object>
                    {
                        ["characterName"] = taskConfig.CharacterName,
                        ["summaryPath"] = summaryPath
                    }
                },
                Progress = new ProgressState
                {
                    TotalSteps = 1,
                    CurrentStep = 0,
                    CurrentPhase = "generating",
                    PendingItems = new List<int> { 0 }
                },
                TaskState = new SkillsTaskState
                {
                    SummaryFilePath = summaryPath
                }
            };
            await checkpointStore.SaveAsync(checkpoint, ct);
        }
        else
        {
            _logger.LogInformation("发现已有 Checkpoint，状态={Status}", checkpoint.Metadata.Status);

            if (checkpoint.Metadata.Status == CheckpointStatus.Completed)
            {
                _logger.LogInformation("Checkpoint 已完成，跳过 Skills 生成");
                return;
            }
        }

        // 4. 调用 LLM 生成技能包
        var isTemplate = taskConfig.OutputMode.Equals("template", StringComparison.OrdinalIgnoreCase);
        var systemPrompt = isTemplate
            ? TemplatePromptBuilder.BuildSystemPrompt(taskConfig.CharacterName)
            : RoleplayPromptBuilder.BuildSystemPrompt(taskConfig.CharacterName);
        var userPrompt = isTemplate
            ? TemplatePromptBuilder.BuildUserPrompt(taskConfig.CharacterName, summaryText)
            : RoleplayPromptBuilder.BuildUserPrompt(taskConfig.CharacterName, summaryText);

        string response;
        try
        {
            Console.WriteLine("[Skills] 开始流式生成...");

            response = await _llmClient.CompleteAsync(systemPrompt, userPrompt, ct);
        }
        catch (Exception ex)
        {
            checkpoint.Metadata.Status = CheckpointStatus.Failed;
            await checkpointStore.SaveAsync(checkpoint, ct);
            _logger.LogError(ex, "Skills 生成失败");
            throw;
        }

        // 5. 解析结果
        Dictionary<string, string> files;
        try
        {
            files = ParseSkillPack(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Skills 结果解析失败，原始响应前 2000 字符：\n{Response}", response[..Math.Min(2000, response.Length)]);
            throw;
        }

        // 6. 保存生成文件
        var (mainDir, codeDir) = isTemplate
            ? (
                Path.Combine(taskConfig.OutputDirectory, "templates", $"{Sanitize(taskConfig.CharacterName)}-template-main"),
                Path.Combine(taskConfig.OutputDirectory, "templates", $"{Sanitize(taskConfig.CharacterName)}-template-code")
              )
            : (
                Path.Combine(taskConfig.OutputDirectory, "roleplay", $"{Sanitize(taskConfig.CharacterName)}-roleplay-main"),
                Path.Combine(taskConfig.OutputDirectory, "roleplay", $"{Sanitize(taskConfig.CharacterName)}-roleplay-code")
              );

        await WriteSkillPackAsync(mainDir, codeDir, files, isTemplate ? "template" : "roleplay", ct);

        // 7. 标记完成
        checkpoint.Metadata.Status = CheckpointStatus.Completed;
        checkpoint.TaskState.FinalOutputPath = mainDir;
        checkpoint.Progress.CompletedItems.Add(0);
        checkpoint.Progress.PendingItems.Remove(0);
        checkpoint.Progress.CurrentStep = 1;
        checkpoint.Progress.CurrentPhase = "completed";
        await checkpointStore.SaveAsync(checkpoint, ct);

        if (isTemplate)
        {
            _logger.LogInformation("模板生成完成，输出目录：{MainDir}", mainDir);
        }
        else
        {
            _logger.LogInformation("Roleplay 生成完成，主目录：{MainDir}，Code 目录：{CodeDir}", mainDir, codeDir);
        }
    }

    private static Dictionary<string, string> ParseSkillPack(string response)
    {
        var trimmed = response.Trim();

        // 策略1: 直接解析裸 JSON
        if (TryParseJsonDict(trimmed, out var directResult))
            return directResult;

        // 策略2: 从 markdown 代码块中提取深度匹配的 JSON
        var extracted = ExtractBalancedJson(trimmed);
        if (extracted != null && TryParseJsonDict(extracted, out var extractedResult))
            return extractedResult;

        throw new InvalidOperationException("无法从 LLM 响应中解析出合法 JSON");
    }

    private static bool TryParseJsonDict(string text, out Dictionary<string, string> result)
    {
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(text, JsonOptions);
            if (dict != null && dict.Count > 0)
            {
                result = dict;
                return true;
            }
        }
        catch { }

        result = new Dictionary<string, string>();
        return false;
    }

    /// <summary>
    /// 从文本中提取首个花括号深度匹配的 JSON 片段，能正确处理字符串内的 { 和 }。
    /// </summary>
    private static string? ExtractBalancedJson(string text)
    {
        int start = text.IndexOf('{');
        if (start < 0) return null;

        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                if (escape)
                {
                    escape = false;
                }
                else if (c == '\\')
                {
                    escape = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }
            }
            else
            {
                if (c == '"')
                {
                    inString = true;
                }
                else if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return text[start..(i + 1)];
                    }
                }
            }
        }

        return null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private async Task WriteSkillPackAsync(string mainDir, string codeDir, Dictionary<string, string> files, string logPrefix, CancellationToken ct)
    {
        Directory.CreateDirectory(mainDir);
        Directory.CreateDirectory(codeDir);

        foreach (var (relativePath, content) in files)
        {
            var mainFilePath = Path.Combine(mainDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(mainFilePath)!);
            await AtomicFileWriter.WriteAllTextAsync(mainFilePath, content, ct);
            _logger.LogInformation("写入 {Prefix} 文件：{Path}", logPrefix, mainFilePath);

            if (!relativePath.Equals("limit.md", StringComparison.OrdinalIgnoreCase))
            {
                var codeFilePath = Path.Combine(codeDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(codeFilePath)!);
                await AtomicFileWriter.WriteAllTextAsync(codeFilePath, content, ct);
            }
        }
    }

    /// <summary>
    /// 将超长 summary 按段落切分，逐段调用 LLM 提取角色关键信息，合并为精炼版 summary。
    /// </summary>
    private async Task<string> CompressSummaryAsync(string characterName, string summaryText, int maxChunkChars, CancellationToken ct)
    {
        // 按段落切分（优先按 \n\n 分割，保留段落完整性）
        var paragraphs = summaryText.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var currentChunk = new StringBuilder();

        foreach (var para in paragraphs)
        {
            if (currentChunk.Length + para.Length > maxChunkChars && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString());
                currentChunk.Clear();
            }
            currentChunk.AppendLine(para);
            currentChunk.AppendLine();
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString());
        }

        _logger.LogInformation("Summary 分片完成，共 {Count} 段", chunks.Count);

        var systemPrompt = $$"""
            你是一个专业的文本提炼助手。
            你的任务是从给定的文本片段中提取关于特定角色的所有关键信息。
            保留对角色性格、行为模式、语言风格、人际关系、重要经历有描述价值的细节。
            去除与角色无关的剧情铺垫、场景描写、环境描写。
            输出为结构化的中文要点列表，每条要点尽量完整，不要过度精简。
            """;

        var compressedParts = new List<string>();
        for (int i = 0; i < chunks.Count; i++)
        {
            _logger.LogInformation("正在压缩第 {Index}/{Total} 段 summary...", i + 1, chunks.Count);

            var userPrompt = $$"""
                请从以下文本中提取关于「{{characterName}}」的关键角色信息：

                ---
                {{chunks[i]}}
                ---

                要求：
                1. 只保留与 {{characterName}} 直接相关的内容
                2. 保留性格特征、行为习惯、说话方式、人际关系、重要经历
                3. 用中文要点列表输出
                4. 不要添加总结性评价，只保留原文证据
                """;

            Console.WriteLine($"[Summary 压缩] 第 {i + 1}/{chunks.Count} 段开始流式生成...");

            var part = await _llmClient.CompleteAsync(systemPrompt, userPrompt, ct);
            compressedParts.Add(part);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# {characterName}");
        sb.AppendLine();

        for (int i = 0; i < compressedParts.Count; i++)
        {
            sb.AppendLine(compressedParts[i]);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string Sanitize(string name)
    {
        return string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
    }
}
