using HwSync.Abstractions.Models;
using HwSync.Infrastructure.FileSystem;
namespace HwSync.Core.Tests.Infrastructure
{
    /// <summary>
    /// Проверки безопасного копирования файлов.
    /// </summary>
    public class SyncStorageTests
    {
        /// <summary>
        /// Сбой удаления временного файла не подменяет ошибку загрузки или отмену.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        [Platform("Win")]
        public async Task Copy_CleanupFailurePreservesOriginalOutcome(bool cancelled)
        {
            string directory = Path.Combine(Path.GetTempPath(), "HwSyncCleanup-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            using CancellationTokenSource cancellation = new();
            MissingFileSynchronizer copy = new();
            OperationCanceledException originalCancellation = new(cancellation.Token);
            try
            {
                Task<IReadOnlyList<FileCopyResult>> operation = copy.CopyAsync(directory,
                    [new("failed.txt", 1, DateTime.UnixEpoch)],
                    (file, output, token) =>
                    {
                        string temporary = ((FileStream)output).Name;
                        File.SetAttributes(temporary, FileAttributes.ReadOnly);
                        if (cancelled)
                        {
                            cancellation.Cancel();
                            throw originalCancellation;
                        }

                        throw new IOException("Исходная ошибка загрузки");
                    }, cancellation.Token);

                if (cancelled)
                {
                    OperationCanceledException? error = Assert.ThrowsAsync<OperationCanceledException>(async () => await operation);
                    Assert.That(error, Is.SameAs(originalCancellation));
                    Assert.That(error!.CancellationToken, Is.EqualTo(cancellation.Token));
                }
                else
                {
                    IReadOnlyList<FileCopyResult> results = await operation;
                    Assert.That(results.Single().Copied, Is.False);
                    Assert.That(results.Single().Error, Is.EqualTo("Исходная ошибка загрузки"));
                }

                Assert.That(File.Exists(Path.Combine(directory, "failed.txt")), Is.False);
                Assert.That(Directory.GetFiles(directory, ".hwsync-*.tmp"), Has.Length.EqualTo(1));
            }
            finally
            {
                foreach (string temporary in Directory.GetFiles(directory))
                {
                    File.SetAttributes(temporary, FileAttributes.Normal);
                    File.Delete(temporary);
                }

                Directory.Delete(directory);
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
