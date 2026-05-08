using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CharacterKiller.Core.Interfaces;
using CharacterKiller.Core.Models;
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

    public SkillsPipeline(
        ILlmClient llmClient,
        ICheckpointStore checkpointStore,
        ILogger<SkillsPipeline> logger)
    {
        _llmClient = llmClient;
        _baseCheckpointStore = checkpointStore;
        _logger = logger;
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

        // 2. 计算 Checkpoint ID
        var contentHash = ComputeHash(summaryPath + summaryText);
        var checkpointId = $"skills_{Sanitize(taskConfig.CharacterName)}_{contentHash[..8]}";

        // 3. 加载或创建 Checkpoint
        var checkpointDir = Path.Combine(taskConfig.OutputDirectory, "checkpoints");
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
        var systemPrompt = SkillsPromptBuilder.BuildSystemPrompt(taskConfig.CharacterName);
        var userPrompt = SkillsPromptBuilder.BuildUserPrompt(taskConfig.CharacterName, summaryText);

        string response;
        try
        {
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

        // 6. 保存技能包文件
        var outputBaseDir = Path.Combine(taskConfig.OutputDirectory, "skills");
        var mainDir = Path.Combine(outputBaseDir, $"{Sanitize(taskConfig.CharacterName)}-skill-main");
        var codeDir = Path.Combine(outputBaseDir, $"{Sanitize(taskConfig.CharacterName)}-skill-code");

        Directory.CreateDirectory(mainDir);
        Directory.CreateDirectory(codeDir);

        foreach (var (relativePath, content) in files)
        {
            // 写入主目录
            var mainFilePath = Path.Combine(mainDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(mainFilePath)!);
            await File.WriteAllTextAsync(mainFilePath, content, ct);
            _logger.LogInformation("写入技能包文件：{Path}", mainFilePath);

            // 写入 code 目录（排除 limit.md）
            if (!relativePath.Equals("limit.md", StringComparison.OrdinalIgnoreCase))
            {
                var codeFilePath = Path.Combine(codeDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(codeFilePath)!);
                await File.WriteAllTextAsync(codeFilePath, content, ct);
            }
        }

        // 7. 标记完成
        checkpoint.Metadata.Status = CheckpointStatus.Completed;
        checkpoint.TaskState.FinalOutputPath = mainDir;
        checkpoint.Progress.CompletedItems.Add(0);
        checkpoint.Progress.PendingItems.Remove(0);
        checkpoint.Progress.CurrentStep = 1;
        checkpoint.Progress.CurrentPhase = "completed";
        await checkpointStore.SaveAsync(checkpoint, ct);

        _logger.LogInformation("Skills 生成完成，主目录：{MainDir}，Code 目录：{CodeDir}", mainDir, codeDir);
    }

    private static Dictionary<string, string> ParseSkillPack(string response)
    {
        // 清理可能的 markdown 代码块
        var cleaned = response.Trim();
        if (cleaned.StartsWith("```"))
        {
            var start = cleaned.IndexOf('{');
            var end = cleaned.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                cleaned = cleaned[start..(end + 1)];
            }
        }

        var result = JsonSerializer.Deserialize<Dictionary<string, string>>(cleaned, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false
        });

        if (result == null || result.Count == 0)
        {
            throw new InvalidOperationException("Skills JSON 解析结果为空");
        }

        return result;
    }

    private static string ComputeHash(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string Sanitize(string name)
    {
        return string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
    }
}
