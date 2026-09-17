using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using HwSync.Infrastructure.FileSystem;
namespace HwSync.Core.Tests.Infrastructure
{
    /// <summary>Проверки защиты файлов при копировании и удалении.</summary>
    public class ComparedFileOperationsTests
    {
        /// <summary>Проверяет защиту изменённых файлов и очистку неполных загрузок.</summary>
        [Test]
        public async Task Operations_PreserveChangedFilesAndRejectIncompleteUploads()
        {
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "manual-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            IComparedFileOperations files = new ComparedFileOperations();
            DateTime time = new(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc);
            FileSnapshot snapshot = new("copy.txt", 3, time);
            using MemoryStream shortInput = new([1]);
            Assert.ThrowsAsync<IOException>(async () => await files.CopyMissingAsync(root, snapshot, shortInput, CancellationToken.None));
            Assert.That(Directory.GetFiles(root), Is.Empty);
            using MemoryStream longInput = new([1, 2, 3, 4]);
            Assert.ThrowsAsync<IOException>(async () => await files.CopyMissingAsync(root, snapshot, longInput, CancellationToken.None));
            Assert.That(Directory.GetFiles(root), Is.Empty);
            using MemoryStream input = new([1, 2, 3]);
            await files.CopyMissingAsync(root, snapshot, input, CancellationToken.None);
            Assert.Throws<IOException>(() => files.EnsureMissing(root, "copy.txt"));
            File.AppendAllText(Path.Combine(root, "copy.txt"), "changed");
            Assert.Throws<IOException>(() => files.DeleteUnchanged(root, snapshot));
            Assert.That(File.Exists(Path.Combine(root, "copy.txt")), Is.True);
            File.WriteAllBytes(Path.Combine(root, "copy.txt"), [1, 2, 3]);
            File.SetLastWriteTimeUtc(Path.Combine(root, "copy.txt"), time);
            files.DeleteUnchanged(root, snapshot);
            Assert.That(File.Exists(Path.Combine(root, "copy.txt")), Is.False);
            files.EnsureMissing(root, "copy.txt");
            Assert.Catch<IOException>(() => files.EnsureMissing(Path.Combine(root, "missing-root"), "copy.txt"));
        }
    }
}
