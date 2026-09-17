using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Infrastructure.FileSystem;

/// <summary>Чтение снимка папки без перехода по ссылкам и junction.</summary>
public sealed class DirectorySnapshotProvider : IFileSnapshotProvider
{
    /// <summary>Обходит каталог, отклоняя ссылки и точки повторного анализа.</summary>
    private static IEnumerable<string> EnumerateFiles(string rootPath)
    {
        Stack<string> directories = new();
        directories.Push(rootPath);
        while (directories.TryPop(out string? directory))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Ссылки и junction не поддерживаются при полном сканировании.");
            }
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Ссылки и junction не поддерживаются при полном сканировании.");
                }
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    directories.Push(entry);
                }
                else
                {
                    yield return entry;
                }
            }
        }
    }

    /// <summary>Возвращает снимок файлов относительно корневой папки.</summary>
    public IReadOnlyCollection<FileSnapshot> GetSnapshot(string rootPath)
    {
        List<FileSnapshot> snapshots = [];

        foreach (string filePath in EnumerateFiles(
            rootPath))
        {
            FileInfo fileInfo = new(filePath);

            string relativePath =
                Path.GetRelativePath(rootPath, filePath);

            FileSnapshot snapshot = new(
                relativePath,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc);

            snapshots.Add(snapshot);
        }

        return snapshots;
    }
}
