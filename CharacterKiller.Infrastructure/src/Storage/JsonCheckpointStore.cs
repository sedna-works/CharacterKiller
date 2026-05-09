using System.Text.Json;
using CharacterKiller.Core.Interfaces;
using CharacterKiller.Core.Models;
using CharacterKiller.Core.Services;
using Microsoft.Extensions.Logging;

namespace CharacterKiller.Infrastructure.Storage;

/// <summary>
/// 基于 JSON 文件的 Checkpoint 存储实现。
/// 大内容（切片结果）分离到独立文件，checkpoint JSON 仅保存元数据。
/// </summary>
public class JsonCheckpointStore : ICheckpointStore, IDisposable
{
    private readonly string _baseDir;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ILogger<JsonCheckpointStore>? _logger;

    public JsonCheckpointStore(string baseDir, ILogger<JsonCheckpointStore>? logger = null)
    {
        _baseDir = Path.IsPathRooted(baseDir) ? baseDir : Path.Combine(Directory.GetCurrentDirectory(), baseDir);
        _logger = logger;
    }

    public void Dispose()
    {
        _writeLock.Dispose();
    }

    public ICheckpointStore WithBaseDir(string baseDir)
    {
        var resolvedPath = Path.IsPathRooted(baseDir) ? baseDir : Path.Combine(_baseDir, baseDir);
        return new JsonCheckpointStore(resolvedPath, _logger);
    }

    public async Task<CheckpointState<TState>?> LoadAsync<TState>(string checkpointId, CancellationToken ct = default)
        where TState : class, new()
    {
        var path = GetCheckpointPath(checkpointId);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, ct);
        return JsonSerializer.Deserialize<CheckpointState<TState>>(json, JsonOptions);
    }

    public async Task SaveAsync<TState>(CheckpointState<TState> state, CancellationToken ct = default)
        where TState : class, new()
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            state.Metadata.UpdatedAt = DateTime.UtcNow;

            var path = GetCheckpointPath(state.Metadata.CheckpointId);
            var json = JsonSerializer.Serialize(state, JsonOptions);
            await AtomicFileWriter.WriteAllTextAsync(path, json, ct);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteAsync(string checkpointId, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            var path = GetCheckpointPath(checkpointId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            // 清理临时切片结果目录
            var tempDir = GetTempDir(checkpointId);
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }, ct);
    }

    public async Task<IReadOnlyList<CheckpointSummary>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_baseDir))
        {
            return Array.Empty<CheckpointSummary>();
        }

        var files = Directory.EnumerateFiles(_baseDir, "*.json", SearchOption.TopDirectoryOnly);
        var results = new List<CheckpointSummary>();

        foreach (var file in files)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                using var doc = JsonDocument.Parse(json);
                var meta = doc.RootElement.GetProperty("Metadata");

                var statusRaw = meta.GetProperty("Status").GetString() ?? "Failed";
                if (!Enum.TryParse<CheckpointStatus>(statusRaw, out var status))
                {
                    status = CheckpointStatus.Failed;
                }

                results.Add(new CheckpointSummary
                {
                    CheckpointId = meta.GetProperty("CheckpointId").GetString() ?? Path.GetFileNameWithoutExtension(file),
                    TaskType = meta.GetProperty("TaskType").GetString() ?? "unknown",
                    Status = status,
                    CreatedAt = meta.TryGetProperty("CreatedAt", out var ca) ? ca.GetDateTime() : DateTime.MinValue,
                    UpdatedAt = meta.TryGetProperty("UpdatedAt", out var ua) ? ua.GetDateTime() : DateTime.MinValue
                });
            }
            catch (Exception ex)
            {
                // 跳过损坏的文件，但记录日志以便排查
                _logger?.LogWarning(ex, "跳过损坏的 checkpoint 文件：{File}", file);
            }
        }

        return results;
    }

    public async Task SaveSliceResultAsync(string checkpointId, int index, string content, CancellationToken ct = default)
    {
        var path = GetSlicePath(checkpointId, index);
        await AtomicFileWriter.WriteAllTextAsync(path, content, ct);
    }

    public async Task<string?> LoadSliceResultAsync(string checkpointId, int index, CancellationToken ct = default)
    {
        var path = GetSlicePath(checkpointId, index);
        if (!File.Exists(path))
        {
            return null;
        }

        return await File.ReadAllTextAsync(path, ct);
    }

    public Task<bool> SliceResultExistsAsync(string checkpointId, int index, CancellationToken ct = default)
    {
        var path = GetSlicePath(checkpointId, index);
        return Task.FromResult(File.Exists(path) && new FileInfo(path).Length > 0);
    }

    private string GetCheckpointPath(string checkpointId)
    {
        // 避免非法字符
        var safeId = string.Join("_", checkpointId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_baseDir, $"{safeId}.json");
    }

    private string GetTempDir(string checkpointId)
    {
        var safeId = string.Join("_", checkpointId.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_baseDir, "temp", safeId);
    }

    private string GetSlicePath(string checkpointId, int index)
    {
        return Path.Combine(GetTempDir(checkpointId), $"slice_{index}.md");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
