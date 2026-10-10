using HwSync.Abstractions.Models;
using HwSync.Core.Tests.Helpers;
using HwSync.Infrastructure.FileSystem;

namespace HwSync.Core.Tests.Infrastructure;

/// <summary>
/// Проверки чтения файлов и относительных путей.
/// </summary>
[TestFixture]
public class DirectorySnapshotProviderTests
{
    /// <summary>
    /// Исключает служебные файлы, но сохраняет обычные tmp и одноимённые каталоги.
    /// </summary>
    [Test]
    public void GetSnapshot_ExcludesOnlyTransferTemporaryFiles()
    {
        using TemporaryDirectory directory = new();
        string temporaryName = ".hwsync-0123456789abcdef0123456789abcdef.tmp";
        string nested = Path.Combine(directory.Path, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(directory.Path, temporaryName), "partial");
        File.WriteAllText(Path.Combine(nested, temporaryName.ToUpperInvariant()), "abandoned");
        string[] ordinaryNames = ["notes.tmp", ".hwsync-not-a-guid.tmp", ".hwsync-0123456789abcdef0123456789abcdef.tmp.bak"];
        foreach (string name in ordinaryNames)
        {
            File.WriteAllText(Path.Combine(directory.Path, name), "user data");
        }

        string namedDirectory = Path.Combine(directory.Path, ".hwsync-ffffffffffffffffffffffffffffffff.tmp");
        Directory.CreateDirectory(namedDirectory);
        File.WriteAllText(Path.Combine(namedDirectory, "inside.txt"), "user data");
        DirectorySnapshotProvider provider = new();
        IReadOnlyCollection<FileSnapshot> snapshot = provider.GetSnapshot(directory.Path);
        Assert.That(snapshot.Select(file => file.RelativePath), Is.EquivalentTo(
            ordinaryNames.Append(Path.Combine(".hwsync-ffffffffffffffffffffffffffffffff.tmp", "inside.txt"))));
    }

    /// <summary>
    /// Проверяет чтение атрибутов файла в папке.
    /// </summary>
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

    /// <summary>
    /// Проверяет относительный путь вложенного файла.
    /// </summary>
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

    /// <summary>
    /// Проверяет снимок пустой папки.
    /// </summary>
    [Test]
    public void GetSnapshot_WhenDirectoryIsEmpty_ReturnsEmptyCollection()
    {
        using TemporaryDirectory temporaryDirectory = new();

        DirectorySnapshotProvider provider = new();

        IReadOnlyCollection<FileSnapshot> snapshots =
            provider.GetSnapshot(temporaryDirectory.Path);

        Assert.That(snapshots, Is.Empty);
    }

    /// <summary>
    /// Проверяет включение всех файлов в снимок.
    /// </summary>
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

        string[] relativePaths = [.. snapshots.Select(snapshot => snapshot.RelativePath)];

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
