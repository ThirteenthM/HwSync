using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.FileSystem;

/// <summary>
/// Чтение атрибутов файлов указанной папки.
/// </summary>
public interface IFileSnapshotProvider
{
    /// <summary>
    /// Возвращает снимок файлов относительно корневой папки.
    /// </summary>
    IReadOnlyCollection<FileSnapshot> GetSnapshot(string rootPath, CancellationToken cancellationToken = default);
}
