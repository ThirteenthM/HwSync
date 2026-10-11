using System.Diagnostics;
using HwSync.Abstractions.Models;
using HwSync.Infrastructure.FileSystem;

namespace HwSync.Core.Tests.Infrastructure
{
    /// <summary>
    /// Проверки сохранности оригинала при замене конфликтующей версии.
    /// </summary>
    public sealed class ConflictFileOperationsTests
    {
        /// <summary>
        /// Проверяет успешную замену и отказ при изменении, ошибке размера или отмене.
        /// </summary>
        [TestCase("success")]
        [TestCase("changed-before")]
        [TestCase("changed-during")]
        [TestCase("wrong-size")]
        [TestCase("cancel")]
        public async Task Replace_PreservesOriginalOnFailure(string mode)
        {
            string root = Path.Combine(Path.GetTempPath(), "HwSyncConflict-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "file.txt");
            await File.WriteAllTextAsync(path, "original");
            FileSnapshot before = new("file.txt", 8, File.GetLastWriteTimeUtc(path));
            FileSnapshot incoming = new("file.txt", mode == "wrong-size" ? 100 : 3, DateTime.UnixEpoch);
            using CancellationTokenSource cancellation = new();
            using StringWriter trace = new();
            using TextWriterTraceListener listener = new(trace);
            Trace.Listeners.Add(listener);
            try
            {
                if (mode == "changed-before")
                {
                    File.WriteAllText(path, "newer original");
                }

                ConflictFileOperations operations = new(new SourceFileReader());
                Task replace = operations.ReplaceAsync(root, before, incoming, async (output, token) =>
                {
                    if (mode == "changed-during")
                    {
                        File.WriteAllText(path, "newer original");
                    }

                    await output.WriteAsync("new"u8.ToArray(), token);
                    if (mode == "cancel")
                    {
                        cancellation.Cancel();
                    }
                }, cancellation.Token);
                if (mode == "success")
                {
                    await replace;
                    Assert.That(File.ReadAllText(path), Is.EqualTo("new"));
                    Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(incoming.LastWriteTimeUtc));
                    Assert.That(trace.ToString(), Does.Not.Contain("временный файл"));
                }
                else
                {
                    if (mode == "cancel")
                    {
                        OperationCanceledException? exception = Assert.CatchAsync<OperationCanceledException>(async () => await replace);
                        Assert.That(exception!.CancellationToken, Is.EqualTo(cancellation.Token));
                    }
                    else
                    {
                        Assert.ThrowsAsync<IOException>(async () => await replace);
                    }

                    Assert.That(File.ReadAllText(path), Is.EqualTo(mode.StartsWith("changed") ? "newer original" : "original"));
                }

                Assert.That(Directory.GetFiles(root, ".hwsync-*.tmp"), Is.Empty);
            }
            finally
            {
                Trace.Listeners.Remove(listener);
                foreach (string file in Directory.GetFiles(root))
                {
                    File.Delete(file);
                }

                Directory.Delete(root);
            }
        }
    }
}
