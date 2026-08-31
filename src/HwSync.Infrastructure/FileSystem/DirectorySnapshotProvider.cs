using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Infrastructure.FileSystem;

public sealed class DirectorySnapshotProvider : IFileSnapshotProvider
{
    public IReadOnlyCollection<FileSnapshot> GetSnapshot(string rootPath)
    {
        List<FileSnapshot> snapshots = new();

        foreach (string filePath in Directory.EnumerateFiles(
            rootPath,
            "*",
            SearchOption.AllDirectories))
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
