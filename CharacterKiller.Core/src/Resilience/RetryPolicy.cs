using Microsoft.Extensions.Logging;

namespace CharacterKiller.Core.Resilience;

/// <summary>
/// 通用重试策略，支持指数退避和自定义重试条件。
/// </summary>
public static class RetryPolicy
{
    /// <summary>
    /// 执行带重试的操作。
    /// </summary>
    /// <param name="action">要执行的操作。</param>
    /// <param name="maxRetries">最大重试次数。</param>
    /// <param name="canRetry">判断异常是否值得重试的回调。返回 true 表示重试，false 表示直接抛出。</param>
    /// <param name="logger">日志记录器。</param>
    /// <param name="ct">取消令牌。</param>
    public static async Task<T> ExecuteAsync<T>(
        Func<Task<T>> action,
        int maxRetries,
        Func<Exception, bool> canRetry,
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
            catch (Exception ex) when (canRetry(ex))
            {
                lastException = ex;
            }
        }

        throw new InvalidOperationException($"操作在 {maxRetries} 次重试后仍然失败", lastException);
    }

    /// <summary>
    /// 执行带重试的无返回值操作。
    /// </summary>
    public static async Task ExecuteAsync(
        Func<Task> action,
        int maxRetries,
        Func<Exception, bool> canRetry,
        ILogger logger,
        CancellationToken ct = default)
    {
        await ExecuteAsync(async () => { await action(); return true; }, maxRetries, canRetry, logger, ct);
    }
}
