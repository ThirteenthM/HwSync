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
        string rootPath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString());

        Directory.CreateDirectory(rootPath);

        try
        {
            string filePath = Path.Combine(rootPath, "Test.txt");
            File.WriteAllText(filePath, "Hello HwSync!");

            FileInfo fileInfo = new(filePath);

            DirectorySnapshotProvider provider = new();

            IReadOnlyCollection<FileSnapshot> snapshots =
                provider.GetSnapshot(rootPath);

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
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Test]
    public void GetSnapshot_WhenFileIsNested_ReturnsRelativePath()
    {
        string rootPath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString());

        string directoryPath = Path.Combine(
            rootPath,
            "Photos",
            "2026");

        Directory.CreateDirectory(directoryPath);

        try
        {
            string filePath = Path.Combine(
                directoryPath,
                "Test.jpg");

            File.WriteAllText(filePath, "Hello HwSync!");

            DirectorySnapshotProvider provider = new();

            IReadOnlyCollection<FileSnapshot> snapshots =
                provider.GetSnapshot(rootPath);

            FileSnapshot snapshot = snapshots.Single();

            string expectedRelativePath = Path.Combine(
                "Photos",
                "2026",
                "Test.jpg");

            Assert.That(
                snapshot.RelativePath,
                Is.EqualTo(expectedRelativePath));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Test]
    public void GetSnapshot_WhenDirectoryIsEmpty_ReturnsEmptyCollection()
    {
        using TemporaryDirectory temporaryDirectory = new();

        DirectorySnapshotProvider provider = new();

        IReadOnlyCollection<FileSnapshot> snapshots =
            provider.GetSnapshot(temporaryDirectory.Path);

        Assert.That(snapshots, Is.Empty);
    }

    [Test]
    public void GetSnapshot_WhenDirectoryContainsMultipleFiles_ReturnsAllFiles()
    {
        using TemporaryDirectory temporaryDirectory = new();

        string photosPath = Path.Combine(
            temporaryDirectory.Path,
            "Photos");

        string yearPath = Path.Combine(
            photosPath,
            "2026");

        Directory.CreateDirectory(yearPath);

        File.WriteAllText(
            Path.Combine(temporaryDirectory.Path, "Root.txt"),
            "Root");

        File.WriteAllText(
            Path.Combine(photosPath, "Photo.txt"),
            "Photo");

        File.WriteAllText(
            Path.Combine(yearPath, "Summer.jpg"),
            "Summer");

        DirectorySnapshotProvider provider = new();

        IReadOnlyCollection<FileSnapshot> snapshots =
            provider.GetSnapshot(temporaryDirectory.Path);

        string[] relativePaths = snapshots
            .Select(snapshot => snapshot.RelativePath)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(snapshots, Has.Count.EqualTo(3));

            Assert.That(relativePaths, Does.Contain("Root.txt"));

            Assert.That(
                relativePaths,
                Does.Contain(Path.Combine("Photos", "Photo.txt")));

            Assert.That(
                relativePaths,
                Does.Contain(Path.Combine("Photos", "2026", "Summer.jpg")));
        });
    }
}
