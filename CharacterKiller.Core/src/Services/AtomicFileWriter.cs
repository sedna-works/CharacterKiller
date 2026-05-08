namespace CharacterKiller.Core.Services;

/// <summary>
/// 提供原子性文件写入工具：先写入临时文件，再通过 Move 覆盖目标文件，
/// 避免写入过程中进程崩溃导致半写文件。
/// </summary>
public static class AtomicFileWriter
{
    /// <summary>
    /// 以原子方式将文本写入指定路径。
    /// </summary>
    public static async Task WriteAllTextAsync(string path, string content, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = path + ".tmp" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            await File.WriteAllTextAsync(tempPath, content, ct);
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            // 清理临时文件（如果还存在）
            try { File.Delete(tempPath); } catch { }
            throw;
        }
    }
}
