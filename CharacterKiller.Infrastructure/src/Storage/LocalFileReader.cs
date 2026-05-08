using CharacterKiller.Core.Interfaces;

namespace CharacterKiller.Infrastructure.Storage;

/// <summary>
/// 本地文件读取实现。
/// </summary>
public class LocalFileReader : IFileReader
{
    public Task<string> ReadAsync(string path, CancellationToken ct = default)
    {
        var fileInfo = new FileInfo(path);
        if (fileInfo.Exists && fileInfo.Length > 500 * 1024 * 1024)
        {
            throw new InvalidOperationException($"文件过大（{fileInfo.Length / 1024 / 1024} MB），请确保输入文件不超过 500 MB。路径：{path}");
        }

        return File.ReadAllTextAsync(path, ct);
    }

    public bool Exists(string path)
    {
        return File.Exists(path);
    }
}
