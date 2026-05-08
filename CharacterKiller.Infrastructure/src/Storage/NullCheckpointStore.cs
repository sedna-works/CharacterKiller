using CharacterKiller.Core.Interfaces;
using CharacterKiller.Core.Models;

namespace CharacterKiller.Infrastructure.Storage;

/// <summary>
/// 空实现：当 Checkpoint 被禁用时使用，所有操作均为无操作。
/// </summary>
public class NullCheckpointStore : ICheckpointStore
{
    public Task<CheckpointState<TState>?> LoadAsync<TState>(string checkpointId, CancellationToken ct = default)
        where TState : class, new()
    {
        return Task.FromResult<CheckpointState<TState>?>(null);
    }

    public Task SaveAsync<TState>(CheckpointState<TState> state, CancellationToken ct = default)
        where TState : class, new()
    {
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string checkpointId, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CheckpointSummary>> ListAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<CheckpointSummary>>(Array.Empty<CheckpointSummary>());
    }

    public Task SaveSliceResultAsync(string checkpointId, int index, string content, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task<string?> LoadSliceResultAsync(string checkpointId, int index, CancellationToken ct = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<bool> SliceResultExistsAsync(string checkpointId, int index, CancellationToken ct = default)
    {
        return Task.FromResult(false);
    }

    public ICheckpointStore WithBaseDir(string baseDir)
    {
        return this;
    }
}
