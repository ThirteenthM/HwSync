using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.FileSystem;

public interface IFileSnapshotProvider
{
    IReadOnlyCollection<FileSnapshot> GetSnapshot(string rootPath);
}
