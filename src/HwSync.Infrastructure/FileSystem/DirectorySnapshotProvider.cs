using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Infrastructure.FileSystem;

/// <summary>
/// Чтение снимка папки без перехода по ссылкам и junction.
/// </summary>
public sealed class DirectorySnapshotProvider : IFileSnapshotProvider
{
    /// <summary>
    /// Обходит каталог, отклоняя ссылки и точки повторного анализа.
    /// </summary>
    private static IEnumerable<string> EnumerateFiles(string rootPath, CancellationToken cancellationToken)
    {
        Stack<string> directories = new();
        cancellationToken.ThrowIfCancellationRequested();
        directories.Push(rootPath);
        while (directories.TryPop(out string? directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfReparsePoint(File.GetAttributes(directory));
            foreach (FileSystemInfo entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Атрибуты получены при перечислении: временный файл может уже исчезнуть.
                FileAttributes attributes = entry.Attributes;
                ThrowIfReparsePoint(attributes);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    directories.Push(entry.FullName);
                }
                else if (!TransferTemporaryFile.IsFileName(entry.Name))
                {
                    yield return entry.FullName;
                }
            }
        }
    }

    /// <summary>
    /// Прерывает сканирование при обнаружении точки повторного анализа.
    /// </summary>
    private static void ThrowIfReparsePoint(FileAttributes attributes)
    {
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Ссылки и junction не поддерживаются при полном сканировании.");
        }
    }

    /// <summary>
    /// Возвращает снимок файлов относительно корневой папки.
    /// </summary>
    public IReadOnlyCollection<FileSnapshot> GetSnapshot(string rootPath, CancellationToken cancellationToken = default)
    {
        List<FileSnapshot> snapshots = [];

        foreach (string filePath in EnumerateFiles(rootPath, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileInfo fileInfo = new(filePath);

            string relativePath =
                Path.GetRelativePath(rootPath, filePath);

            FileSnapshot snapshot = new(
                relativePath,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc);

            snapshots.Add(snapshot);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return snapshots;
    }
}
