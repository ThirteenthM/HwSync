using HwSync.Abstractions.Models;
using HwSync.Core.Models;
using HwSync.Core.Services;
using HwSync.Core.Tests.Helpers;
using HwSync.Infrastructure.FileSystem;

namespace HwSync.Core.Tests.Infrastructure;

[TestFixture]
public class SnapshotChangeIntegrationTests
{
    [Test]
    public void Compare_WhenFileChangedBetweenSnapshots_ReturnsModifiedChange()
    {
        using TemporaryDirectory temporaryDirectory = new();

        string filePath = Path.Combine(
            temporaryDirectory.Path,
            "Test.txt");

        File.WriteAllText(filePath, "First version");

        DirectorySnapshotProvider provider = new();

        IReadOnlyCollection<FileSnapshot> previous =
            provider.GetSnapshot(temporaryDirectory.Path);

        File.WriteAllText(
            filePath,
            "Second version with different length");

        IReadOnlyCollection<FileSnapshot> current =
            provider.GetSnapshot(temporaryDirectory.Path);

        ChangeComparer comparer = new();

        IReadOnlyCollection<FileChange> changes =
            comparer.Compare(previous, current);

        Assert.That(changes, Has.Count.EqualTo(1));

        FileChange change = changes.Single();

        Assert.That(
            change.ChangeType,
            Is.EqualTo(FileChangeType.Modified));
    }

    [Test]
    public void Compare_WhenFileCreatedBetweenSnapshots_ReturnsCreatedChange()
    {
        using TemporaryDirectory temporaryDirectory = new();

        DirectorySnapshotProvider provider = new();

        IReadOnlyCollection<FileSnapshot> previous =
            provider.GetSnapshot(temporaryDirectory.Path);

        string filePath = Path.Combine(
            temporaryDirectory.Path,
            "NewFile.txt");

        File.WriteAllText(filePath, "New file");

        IReadOnlyCollection<FileSnapshot> current =
            provider.GetSnapshot(temporaryDirectory.Path);

        ChangeComparer comparer = new();

        IReadOnlyCollection<FileChange> changes =
            comparer.Compare(previous, current);

        Assert.That(changes, Has.Count.EqualTo(1));

        FileChange change = changes.Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                change.ChangeType,
                Is.EqualTo(FileChangeType.Created));

            Assert.That(
                change.Current?.RelativePath,
                Is.EqualTo("NewFile.txt"));

            Assert.That(
                change.Previous,
                Is.Null);
        });
    }

    [Test]
    public void Compare_WhenFileDeletedBetweenSnapshots_ReturnsDeletedChange()
    {
        using TemporaryDirectory temporaryDirectory = new();

        string filePath = Path.Combine(
            temporaryDirectory.Path,
            "DeletedFile.txt");

        File.WriteAllText(filePath, "File to delete");

        DirectorySnapshotProvider provider = new();

        IReadOnlyCollection<FileSnapshot> previous =
            provider.GetSnapshot(temporaryDirectory.Path);

        File.Delete(filePath);

        IReadOnlyCollection<FileSnapshot> current =
            provider.GetSnapshot(temporaryDirectory.Path);

        ChangeComparer comparer = new();

        IReadOnlyCollection<FileChange> changes =
            comparer.Compare(previous, current);

        Assert.That(changes, Has.Count.EqualTo(1));

        FileChange change = changes.Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                change.ChangeType,
                Is.EqualTo(FileChangeType.Deleted));

            Assert.That(
                change.Previous?.RelativePath,
                Is.EqualTo("DeletedFile.txt"));

            Assert.That(
                change.Current,
                Is.Null);
        });
    }
}
