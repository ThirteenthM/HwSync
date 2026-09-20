using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Infrastructure.FileSystem
{
    /// <summary>
    /// Замена файла после получения полной версии и проверки исходного состояния.
    /// </summary>
    public sealed class ConflictFileOperations : IConflictFileOperations
    {
        private readonly ISourceFileReader _reader;

        /// <summary>
        /// Принимает проверяемое чтение исходных файлов.
        /// </summary>
        public ConflictFileOperations(ISourceFileReader reader)
        {
            _reader = reader;
        }

        /// <summary>
        /// Сохраняет оригинал при ошибке, отмене или изменении после сравнения.
        /// </summary>
        public async Task ReplaceAsync(string root, FileSnapshot expected, FileSnapshot incoming,
            Func<Stream, CancellationToken, Task> receive, CancellationToken token)
        {
            using (Stream original = _reader.OpenRead(root, expected))
            {
                token.ThrowIfCancellationRequested();
            }

            string target = SafeFilePath.Resolve(root, expected.RelativePath);
            string temporary = Path.Combine(Path.GetDirectoryName(target)!, ".hwsync-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await receive(output, token);
                    await output.FlushAsync(token);
                    if (output.Length != incoming.Size)
                    {
                        throw new IOException("Размер новой версии отличается от снимка.");
                    }
                }

                token.ThrowIfCancellationRequested();
                File.SetLastWriteTimeUtc(temporary, incoming.LastWriteTimeUtc);
                SafeFilePath.Resolve(root, expected.RelativePath);
                using (Stream original = _reader.OpenRead(root, expected))
                {
                    token.ThrowIfCancellationRequested();
                }
                File.Move(temporary, target, true);
            }
            finally
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    System.Diagnostics.Trace.TraceWarning("Не удалось удалить временный файл {0}: {1}", temporary, exception.Message);
                }
            }
        }
    }
}
