namespace CharacterKiller.Core.Interfaces;

/// <summary>
/// 文件读取抽象。
/// </summary>
public interface IFileReader
{
    Task<string> ReadAsync(string path, CancellationToken ct = default);

    bool Exists(string path);
}
