using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Client.Tests
{
    internal sealed class StubSnapshotProvider : IFileSnapshotProvider
    {
        public IReadOnlyCollection<FileSnapshot> GetSnapshot(string rootPath) =>
            [new("client.txt", 12, new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc))];
    }
}