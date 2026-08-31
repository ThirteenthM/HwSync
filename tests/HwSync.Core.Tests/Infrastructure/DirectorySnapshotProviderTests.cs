using HwSync.Abstractions.Models;
using HwSync.Core.Tests.Helpers;
using HwSync.Infrastructure.FileSystem;

namespace HwSync.Core.Tests.Infrastructure;

[TestFixture]
public class DirectorySnapshotProviderTests
{
    [Test]
    public void GetSnapshot_WhenDirectoryContainsFile_ReturnsFileSnapshot()
    {
        using TemporaryDirectory temporaryDirectory = new();

        string filePath = Path.Combine(temporaryDirectory.Path, "Test.txt");
        File.WriteAllText(filePath, "Hello HwSync!");

        FileInfo fileInfo = new(filePath);

        DirectorySnapshotProvider provider = new();

        IReadOnlyCollection<FileSnapshot> snapshots =
            provider.GetSnapshot(temporaryDirectory.Path);

        Assert.That(snapshots, Has.Count.EqualTo(1));

        FileSnapshot snapshot = snapshots.Single();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.RelativePath, Is.EqualTo("Test.txt"));
            Assert.That(snapshot.Size, Is.EqualTo(fileInfo.Length));
            Assert.That(
                snapshot.LastWriteTimeUtc,
                Is.EqualTo(fileInfo.LastWriteTimeUtc));
        });
    }

    [Test]
    public void GetSnapshot_WhenFileIsNested_ReturnsRelativePath()
    {
        using TemporaryDirectory temporaryDirectory = new();

        string directoryPath = Path.Combine(
            temporaryDirectory.Path,
            "Photos",
            "2026");

        Directory.CreateDirectory(directoryPath);

        string filePath = Path.Combine(
            directoryPath,
            "Test.jpg");

        File.WriteAllText(filePath, "Hello HwSync!");

        DirectorySnapshotProvider provider = new();

        IReadOnlyCollection<FileSnapshot> snapshots =
            provider.GetSnapshot(temporaryDirectory.Path);

        FileSnapshot snapshot = snapshots.Single();

        string expectedRelativePath = Path.Combine(
            "Photos",
            "2026",
            "Test.jpg");

        Assert.That(
            snapshot.RelativePath,
            Is.EqualTo(expectedRelativePath));
    }
}
