using HwSync.Abstractions.Services;
using HwSync.Abstractions.Models;
using HwSync.Core.Services;

namespace HwSync.Core.Tests.Services
{
    /// <summary>
    /// Проверки взаимодействия сканера с поставщиком снимков.
    /// </summary>
    public partial class DirectoryChangeScannerTests
    {
        /// <summary>
        /// Проверяет сравнение снимка именно запрошенной папки.
        /// </summary>
        [Test]
        public void Scan_WhenSnapshotsDiffer_ReturnsChangesForRequestedDirectory()
        {
            FileSnapshot deleted = new("Deleted.txt", 10, LastWriteTimeUtc);
            FileSnapshot previousModified = new("Modified.txt", 10, LastWriteTimeUtc);
            FileSnapshot currentModified = new("Modified.txt", 20, LastWriteTimeUtc);
            FileSnapshot created = new("Created.txt", 10, LastWriteTimeUtc);
            FileSnapshot unchanged = new("Unchanged.txt", 10, LastWriteTimeUtc);
            FileSnapshot[] previous = [deleted, previousModified, unchanged];
            FileSnapshot[] current = [currentModified, created, unchanged];
            StubFileSnapshotProvider provider = new(current);
            IChangeScanner scanner = new DirectoryChangeScanner(provider, new ChangeComparer());
            ChangeScanRequest request = new("requested-directory", previous);

            IReadOnlyCollection<FileChange> changes = scanner.Scan(request);

            FileChange[] expected =
            [
                new(FileChangeType.Deleted, deleted, null),
                new(FileChangeType.Modified, previousModified, currentModified),
                new(FileChangeType.Created, null, created)
            ];

            Assert.Multiple(() =>
            {
                Assert.That(provider.RequestedRootPaths, Is.EqualTo(new[]
{
 request.RootPath
}));
                Assert.That(changes, Is.EquivalentTo(expected));
            });
        }

        /// <summary>
        /// Проверяет чтение свежего снимка при повторном запросе.
        /// </summary>
        [Test]
        public void Scan_WhenCalledAgain_UsesNewRequestAndFreshSnapshot()
        {
            FileSnapshot first = new("First.txt", 10, LastWriteTimeUtc);
            FileSnapshot second = new("Second.txt", 20, LastWriteTimeUtc);
            StubFileSnapshotProvider provider = new([first]);
            IChangeScanner scanner = new DirectoryChangeScanner(provider, new ChangeComparer());
            ChangeScanRequest firstRequest = new("first-directory", []);

            IReadOnlyCollection<FileChange> firstChanges = scanner.Scan(firstRequest);

            provider.Snapshot = [second];
            ChangeScanRequest secondRequest = new("second-directory", [second]);

            IReadOnlyCollection<FileChange> secondChanges = scanner.Scan(secondRequest);

            Assert.Multiple(() =>
            {
                Assert.That(firstChanges, Is.EqualTo(new FileChange[]
                {
                    new(FileChangeType.Created, null, first)
                }));
                Assert.That(secondChanges, Is.Empty);
                Assert.That(provider.RequestedRootPaths,
                    Is.EqualTo(new[]
{
 firstRequest.RootPath, secondRequest.RootPath
}));
            });
        }
    }
}
