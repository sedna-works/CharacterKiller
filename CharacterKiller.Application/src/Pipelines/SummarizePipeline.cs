using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CharacterKiller.Core.Interfaces;
using CharacterKiller.Core.Models;
using CharacterKiller.Core.Services;
using CharacterKiller.Application.Prompts;
using Microsoft.Extensions.Logging;

namespace CharacterKiller.Application.Pipelines;

/// <summary>
/// Summarize 流程编排器：文本切片 → 逐段归纳 → 汇总保存。
/// 支持切片级并发执行，通过 <see cref="ExecutionConfig.MaxChunkConcurrency"/> 控制。
/// </summary>
public class SummarizePipeline
{
    private readonly ILlmClient _llmClient;
    private readonly ITokenEstimator _estimator;
    private readonly ICheckpointStore _baseCheckpointStore;
    private readonly IFileReader _fileReader;
    private readonly ILogger<SummarizePipeline> _logger;
    private readonly ExecutionConfig _executionConfig;

    public SummarizePipeline(
        ILlmClient llmClient,
        ITokenEstimator estimator,
        ICheckpointStore checkpointStore,
        IFileReader fileReader,
        ILogger<SummarizePipeline> logger,
        ExecutionConfig executionConfig)
    {
        _llmClient = llmClient;
        _estimator = estimator;
        _baseCheckpointStore = checkpointStore;
        _fileReader = fileReader;
        _logger = logger;
        _executionConfig = executionConfig;
    }

    public async Task RunAsync(TaskConfig taskConfig, SlicingConfig slicingConfig, CancellationToken ct = default)
    {
        _logger.LogInformation("开始 Summarize 流程：角色={Character}，切片并发度={Concurrency}",
            taskConfig.CharacterName, _executionConfig.MaxChunkConcurrency);

        // 使用任务输出目录下的 checkpoints 子目录
        var checkpointDir = Path.Combine(taskConfig.OutputDirectory, "checkpoints");
        var checkpointStore = _baseCheckpointStore.WithBaseDir(checkpointDir);

        // 1. 读取文本（支持单文件或多文件合并）
        string text;
        if (taskConfig.InputFiles.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var file in taskConfig.InputFiles)
            {
                _logger.LogInformation("读取文件：{File}", file);
                sb.AppendLine(await _fileReader.ReadAsync(file, ct));
                sb.AppendLine();
            }
            text = sb.ToString();
            _logger.LogInformation("多文件合并完成，共 {Count} 个文件，总长度={Length} 字符", taskConfig.InputFiles.Count, text.Length);
        }
        else
        {
            text = await _fileReader.ReadAsync(taskConfig.InputFile, ct);
            _logger.LogInformation("文本读取完成，长度={Length} 字符", text.Length);
        }

        // 2. 切片
        var slicer = new TextSlicer(_estimator);
        var chunks = slicer.Slice(text, slicingConfig.ChunkSizeTokens, slicingConfig.OverlapTokens);
        _logger.LogInformation("文本切片完成，共 {Count} 块", chunks.Count);

        // 3. 计算 Checkpoint ID（任务类型 + 角色名 + 文件内容哈希）
        var fileHash = ComputeHash(text);
        var checkpointId = $"summarize_{Sanitize(taskConfig.CharacterName)}_{fileHash[..8]}";

        // 4. 加载或创建 Checkpoint
        var checkpoint = await checkpointStore.LoadAsync<SummarizeTaskState>(checkpointId, ct);
        if (checkpoint == null)
        {
            _logger.LogInformation("创建新 Checkpoint：{CheckpointId}", checkpointId);
            checkpoint = new CheckpointState<SummarizeTaskState>
            {
                Metadata = new CheckpointMetadata
                {
                    CheckpointId = checkpointId,
                    TaskType = "summarize",
                    Status = CheckpointStatus.Running,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    InputParams = new Dictionary<string, object>
                    {
                        ["characterName"] = taskConfig.CharacterName,
                        ["inputFile"] = taskConfig.InputFile,
                        ["chunkSizeTokens"] = slicingConfig.ChunkSizeTokens
                    }
                },
                Progress = new ProgressState
                {
                    TotalSteps = chunks.Count,
                    CurrentPhase = "slicing",
                    PendingItems = Enumerable.Range(0, chunks.Count).ToList()
                },
                TaskState = new SummarizeTaskState
                {
                    Chunks = chunks
                }
            };
            await checkpointStore.SaveAsync(checkpoint, ct);
        }
        else
        {
            _logger.LogInformation("发现已有 Checkpoint，状态={Status}", checkpoint.Metadata.Status);

            if (checkpoint.Metadata.Status == CheckpointStatus.Completed)
            {
                _logger.LogInformation("Checkpoint 已完成，跳过 Summarize");
                return;
            }

            // 5. Sanitize：验证 completed_items 对应的切片文件是否真实存在
            await SanitizeCheckpointAsync(checkpoint, checkpointId, checkpointStore, ct);
        }

        // 6. 执行 pending 切片
        var systemPrompt = SummarizePromptBuilder.BuildSystemPrompt();
        var pending = checkpoint.Progress.PendingItems.ToList(); // 复制避免遍历时修改

        if (_executionConfig.MaxChunkConcurrency <= 1)
        {
            // 顺序执行（默认，向后兼容）
            foreach (var index in pending)
            {
                await ProcessChunkAsync(index, chunks, systemPrompt, checkpoint, checkpointId, checkpointStore, ct);
            }
        }
        else
        {
            // 并发执行
            var progressLock = new object();
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = _executionConfig.MaxChunkConcurrency,
                CancellationToken = ct
            };

            try
            {
                await Parallel.ForEachAsync(pending, options, async (index, innerCt) =>
                {
                    _logger.LogInformation("处理切片 {Index}/{Total}", index + 1, chunks.Count);

                    var chunk = chunks[index];
                    var userPrompt = SummarizePromptBuilder.BuildUserPrompt(taskConfig.CharacterName, chunk.Content);

                    try
                    {
                        var result = await _llmClient.CompleteAsync(systemPrompt, userPrompt, innerCt);

                        // 保存切片结果到独立文件（大内容分离）
                        await checkpointStore.SaveSliceResultAsync(checkpointId, index, result, innerCt);

                        lock (progressLock)
                        {
                            checkpoint.TaskState.SliceOutputFiles[index] = $"slice_{index}.txt";
                            checkpoint.Progress.CompletedItems.Add(index);
                            checkpoint.Progress.PendingItems.Remove(index);
                            checkpoint.Progress.CurrentStep = checkpoint.Progress.CompletedItems.Count;
                        }

                        await checkpointStore.SaveAsync(checkpoint, innerCt);
                        _logger.LogInformation("切片 {Index} 完成", index + 1);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "切片 {Index} 处理失败", index + 1);

                        lock (progressLock)
                        {
                            checkpoint.Progress.FailedItems.Add(index);
                            checkpoint.Progress.PendingItems.Remove(index);
                        }

                        checkpoint.Metadata.Status = CheckpointStatus.Failed;
                        await checkpointStore.SaveAsync(checkpoint, innerCt);
                        throw;
                    }
                });
            }
            catch (AggregateException aex)
            {
                // Parallel.ForEachAsync 会将异常包装为 AggregateException
                // 提取第一个内部异常重新抛出，保持日志简洁
                throw aex.InnerExceptions.FirstOrDefault() ?? aex;
            }
        }

        // 7. 汇总所有结果
        _logger.LogInformation("汇总所有切片结果...");
        var segments = new List<string>();
        foreach (var idx in checkpoint.Progress.CompletedItems.OrderBy(x => x))
        {
            var content = await checkpointStore.LoadSliceResultAsync(checkpointId, idx, ct);
            if (!string.IsNullOrWhiteSpace(content))
            {
                segments.Add(content);
            }
        }

        var outputDir = Path.Combine(taskConfig.OutputDirectory, "summaries");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, $"{Sanitize(taskConfig.CharacterName)}.md");
        var markdown = RenderSummaryMarkdown(taskConfig.CharacterName, segments, taskConfig.InputFiles.Count > 0 ? taskConfig.InputFiles : new List<string> { taskConfig.InputFile });
        await File.WriteAllTextAsync(outputPath, markdown, ct);

        // 8. 标记完成
        checkpoint.Metadata.Status = CheckpointStatus.Completed;
        checkpoint.TaskState.FinalOutputPath = outputPath;
        checkpoint.Progress.CurrentPhase = "completed";
        await checkpointStore.SaveAsync(checkpoint, ct);

        _logger.LogInformation("Summarize 完成，输出：{Path}", outputPath);
    }

    private async Task ProcessChunkAsync(
        int index,
        List<TextChunk> chunks,
        string systemPrompt,
        CheckpointState<SummarizeTaskState> checkpoint,
        string checkpointId,
        ICheckpointStore checkpointStore,
        CancellationToken ct)
    {
        _logger.LogInformation("处理切片 {Index}/{Total}", index + 1, chunks.Count);

        var chunk = chunks[index];
        var userPrompt = SummarizePromptBuilder.BuildUserPrompt(checkpoint.Metadata.InputParams["characterName"]?.ToString() ?? "", chunk.Content);

        try
        {
            var result = await _llmClient.CompleteAsync(systemPrompt, userPrompt, ct);

            // 保存切片结果到独立文件（大内容分离）
            await checkpointStore.SaveSliceResultAsync(checkpointId, index, result, ct);
            checkpoint.TaskState.SliceOutputFiles[index] = $"slice_{index}.txt";
            checkpoint.Progress.CompletedItems.Add(index);
            checkpoint.Progress.PendingItems.Remove(index);
            checkpoint.Progress.CurrentStep = checkpoint.Progress.CompletedItems.Count;

            await checkpointStore.SaveAsync(checkpoint, ct);
            _logger.LogInformation("切片 {Index} 完成", index + 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切片 {Index} 处理失败", index + 1);
            checkpoint.Progress.FailedItems.Add(index);
            checkpoint.Progress.PendingItems.Remove(index);
            checkpoint.Metadata.Status = CheckpointStatus.Failed;
            await checkpointStore.SaveAsync(checkpoint, ct);
            throw;
        }
    }

    /// <summary>
    /// 恢复时校验：检查 CompletedItems 对应的切片文件是否真实存在且有内容。
    /// 若文件缺失，将索引移回 PendingItems。
    /// </summary>
    private async Task SanitizeCheckpointAsync(
        CheckpointState<SummarizeTaskState> checkpoint,
        string checkpointId,
        ICheckpointStore checkpointStore,
        CancellationToken ct)
    {
        var valid = new List<int>();
        var invalid = new List<int>();

        foreach (var index in checkpoint.Progress.CompletedItems)
        {
            var exists = await checkpointStore.SliceResultExistsAsync(checkpointId, index, ct);
            if (exists)
            {
                valid.Add(index);
            }
            else
            {
                invalid.Add(index);
                _logger.LogWarning("Checkpoint 校验失败：切片 {Index} 结果文件缺失，将重新处理", index);
            }
        }

        if (invalid.Count > 0)
        {
            checkpoint.Progress.CompletedItems = valid;
            checkpoint.Progress.PendingItems.AddRange(invalid);
            checkpoint.Progress.PendingItems = checkpoint.Progress.PendingItems.Distinct().OrderBy(x => x).ToList();
            checkpoint.Metadata.Status = CheckpointStatus.Running;
            await checkpointStore.SaveAsync(checkpoint, ct);
        }
    }

    private static string RenderSummaryMarkdown(string characterName, List<string> segments, List<string> inputFiles)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {characterName} - 角色归纳");
        sb.AppendLine();
        sb.AppendLine($"> 生成时间：{DateTime.UtcNow:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"> 来源文本：{string.Join(", ", inputFiles)}");
        sb.AppendLine($"> 归纳片段数：{segments.Count}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        for (int i = 0; i < segments.Count; i++)
        {
            sb.AppendLine($"## 归纳片段 {i + 1}");
            sb.AppendLine();
            sb.AppendLine(segments[i]);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
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
