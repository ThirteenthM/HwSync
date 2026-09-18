using HwSync.Abstractions.Models;
using HwSync.Infrastructure.FileSystem;
namespace HwSync.Core.Tests.Infrastructure
{
    /// <summary>
    /// Проверки истории удалений и безопасного копирования.
    /// </summary>
    public class SyncStorageTests
    {
        /// <summary>
        /// Проверяет сохранение удалений только после исходного снимка.
        /// </summary>
        [Test]
        public void History_PersistsDeletionAndDoesNotInventFirstScanDeletes()
        {
            string directory = Path.Combine(Path.GetTempPath(), "HwSyncHistory-" + Guid.NewGuid());
            string root = Path.Combine(Path.GetTempPath(), "HwSyncSource-" + Guid.NewGuid());
            try
            {
                JsonFolderHistory history = new(directory);
                FileSnapshot file = new("old.txt", 1, DateTime.UnixEpoch);
                history.RecordSnapshot(root, [file]);
                Assert.That(history.GetDeletedFiles(root), Is.Empty);
                history.RecordSnapshot(root, []);
                JsonFolderHistory reloaded = new(directory);
                DeletedFile deleted = reloaded.GetDeletedFiles(root).Single();
                Assert.That(deleted.Deleted, Is.True);
                Assert.That(deleted.ChangeNumber, Is.EqualTo(1));
                Assert.That(deleted.DeletedAtUtc, Is.GreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1)));
                reloaded.RecordSnapshot(root, []);
                Assert.That(reloaded.GetDeletedFiles(root), Has.Count.EqualTo(1));
                reloaded.RecordSnapshot(root, [file]);
                Assert.That(reloaded.GetDeletedFiles(root).Single().Deleted, Is.False);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    foreach (string file in Directory.GetFiles(directory))
                    {
                        File.Delete(file);
                    }
                    Directory.Delete(directory);
                }
            }
        }

        /// <summary>
        /// Проверяет сохранение существующих файлов и очистку неудачной загрузки.
        /// </summary>
        [Test]
        public async Task Copy_PreservesExistingAndCleansFailedDownload()
        {
            string directory = Path.Combine(Path.GetTempPath(), "HwSyncCopy-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, "existing.txt"), "original");
                MissingFileSynchronizer copy = new();
                IReadOnlyList<FileCopyResult> result = await copy.CopyAsync(directory,
                    [new("existing.txt", 1, DateTime.UnixEpoch), new("partial.txt", 10, DateTime.UnixEpoch), new("../escape.txt", 1, DateTime.UnixEpoch)],
                    async (file, output, token) =>
{
    await output.WriteAsync(new byte[]
   {
 1
   }, token);
}, CancellationToken.None);
                Assert.That(result.All(item => !item.Copied), Is.True);
                Assert.That(File.ReadAllText(Path.Combine(directory, "existing.txt")), Is.EqualTo("original"));
                Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
            }
            finally
            {
                foreach (string file in Directory.GetFiles(directory))
                {
                    File.Delete(file);
                }
                Directory.Delete(directory);
            }
        }
    }
}
