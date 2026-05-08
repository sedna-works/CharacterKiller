using Microsoft.Extensions.Logging;

namespace CharacterKiller.Application.Resilience;

/// <summary>
/// 通用重试策略。
/// </summary>
public static class RetryPolicy
{
    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> action,
        int maxRetries,
        ILogger logger,
        CancellationToken ct = default)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    logger.LogWarning("操作失败，第 {Attempt} 次重试，等待 {Delay}s...", attempt, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                }

                return await action();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
            }
        }

        throw new InvalidOperationException($"操作在 {maxRetries} 次重试后仍然失败", lastException);
    }

    public static async Task ExecuteAsync(
        Func<Task> action,
        int maxRetries,
        ILogger logger,
        CancellationToken ct = default)
    {
        await ExecuteAsync(async () => { await action(); return true; }, maxRetries, logger, ct);
    }
}
