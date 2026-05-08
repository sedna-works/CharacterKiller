using CharacterKiller.Core.Models;

namespace CharacterKiller.Core.Interfaces;

/// <summary>
/// Checkpoint 持久化存储抽象。
/// 采用泛型设计，便于后续扩展新任务类型而无需修改接口。
/// </summary>
public interface ICheckpointStore
{
    Task<CheckpointState<TState>?> LoadAsync<TState>(string checkpointId, CancellationToken ct = default)
        where TState : class, new();

    Task SaveAsync<TState>(CheckpointState<TState> state, CancellationToken ct = default)
        where TState : class, new();

    Task DeleteAsync(string checkpointId, CancellationToken ct = default);

    Task<IReadOnlyList<CheckpointSummary>> ListAsync(CancellationToken ct = default);

    // --- 大内容分离存储（Summarize 切片结果） ---

    Task SaveSliceResultAsync(string checkpointId, int index, string content, CancellationToken ct = default);

    Task<string?> LoadSliceResultAsync(string checkpointId, int index, CancellationToken ct = default);

    Task<bool> SliceResultExistsAsync(string checkpointId, int index, CancellationToken ct = default);

    /// <summary>
    /// 创建基于指定目录的 checkpoint store 实例。
    /// 用于将 checkpoint 保存到任务对应的输出目录下，避免污染运行目录。
    /// </summary>
    ICheckpointStore WithBaseDir(string baseDir);
}
