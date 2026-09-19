using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Infrastructure.FileSystem
{
    /// <summary>
    /// Последовательное копирование отсутствующих файлов через временные файлы.
    /// </summary>
    public sealed class MissingFileSynchronizer : IMissingFileSynchronizer
    {
        /// <summary>
        /// Копирует файлы без перезаписи и удаляет незавершённые временные файлы.
        /// </summary>
        public async Task<IReadOnlyList<FileCopyResult>> CopyAsync(string clientRoot, IReadOnlyCollection<FileSnapshot> files, Func<FileSnapshot, Stream, CancellationToken, Task> download, CancellationToken cancellationToken)
        {
            List<FileCopyResult> results = new();
            foreach (FileSnapshot file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? temporary = null;
                try
                {
                    string target = SafeFilePath.Resolve(clientRoot, file.RelativePath);
                    if (File.Exists(target) || Directory.Exists(target))
                    {
                        results.Add(new(file.RelativePath, false, "Файл уже существует; сохранён без изменений."));
                        continue;
                    }

                    string directory = Path.GetDirectoryName(target)!;
                    Directory.CreateDirectory(directory);
                    SafeFilePath.Resolve(clientRoot, file.RelativePath);
                    temporary = Path.Combine(directory, ".hwsync-" + Guid.NewGuid().ToString("N") + ".tmp");
                    await using (FileStream output = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        await download(file, output, cancellationToken);
                        await output.FlushAsync(cancellationToken);
                        if (output.Length != file.Size)
                        {
                            throw new IOException("Размер полученного файла отличается от снимка.");
                        }
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    File.SetLastWriteTimeUtc(temporary, file.LastWriteTimeUtc);
                    SafeFilePath.Resolve(clientRoot, file.RelativePath);
                    File.Move(temporary, target, false);
                    temporary = null;
                    results.Add(new(file.RelativePath, true, null));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Net.Http.HttpRequestException)
                {
                    results.Add(new(file.RelativePath, false, exception.Message));
                }
                finally
                {
                    if (temporary is not null && File.Exists(temporary))
                    {
                        File.Delete(temporary);
                    }
                }
            }

            return results;
        }
    }
}
