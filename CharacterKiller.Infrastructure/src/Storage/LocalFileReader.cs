using CharacterKiller.Core.Interfaces;

namespace CharacterKiller.Infrastructure.Storage;

/// <summary>
/// 本地文件读取实现。
/// </summary>
public class LocalFileReader : IFileReader
{
    public Task<string> ReadAsync(string path, CancellationToken ct = default)
    {
        return File.ReadAllTextAsync(path, ct);
    }

    public bool Exists(string path)
    {
        return File.Exists(path);
    }
}
