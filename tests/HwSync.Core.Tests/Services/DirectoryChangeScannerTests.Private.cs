using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Core.Tests.Services
{
    public partial class DirectoryChangeScannerTests
    {
        private static readonly DateTime LastWriteTimeUtc =
            new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

        private sealed class StubFileSnapshotProvider : IFileSnapshotProvider
        {
            public IReadOnlyCollection<FileSnapshot> Snapshot { get; set; }

            public List<string> RequestedRootPaths { get; } = [];

            public StubFileSnapshotProvider(IReadOnlyCollection<FileSnapshot> snapshot)
            {
                Snapshot = snapshot;
            }

            public IReadOnlyCollection<FileSnapshot> GetSnapshot(string rootPath)
            {
                RequestedRootPaths.Add(rootPath);
                return Snapshot;
            }
        }
    }
}
