using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
namespace HwSync.Infrastructure.FileSystem
{
    /// <summary>
    /// Копирование и удаление с проверкой состояния файлов.
    /// </summary>
    public sealed class ComparedFileOperations : IComparedFileOperations
    {
        /// <summary>
        /// Проверяет отсутствие пути и доступность корневой папки.
        /// </summary>
        public void EnsureMissing(string root, string relativePath)
        {
            if ((File.GetAttributes(root) & FileAttributes.Directory) == 0)
            {
                throw new IOException("Корневая папка недоступна.");
            }
            string path = SafeFilePath.Resolve(root, relativePath);
            try
            {
                File.GetAttributes(path);
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            throw new IOException("Путь уже существует. Повторите сравнение.");
        }

        /// <summary>
        /// Копирует отсутствующий файл без перезаписи существующего.
        /// </summary>
        public async Task CopyMissingAsync(string root, FileSnapshot file, Stream content, CancellationToken token)
        {
            MissingFileSynchronizer copier = new();
            IReadOnlyList<FileCopyResult> results = await copier.CopyAsync(root, [file],
                async (_, output, cancellation) =>
                {
                    byte[] buffer = new byte[81920];
                    long remaining = file.Size;
                    while (remaining > 0)
                    {
                        int read = await content.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellation);
                        if (read == 0)
                        {
                            throw new IOException("Передача файла прервана.");
                        }
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
                        remaining -= read;
                    }
                    if (await content.ReadAsync(buffer.AsMemory(0, 1), cancellation) != 0)
                    {
                        throw new IOException("Размер файла превышает снимок.");
                    }
                }, token);
            if (!results[0].Copied)
            {
                throw new IOException(results[0].Error);
            }
        }

        /// <summary>
        /// Удаляет файл только при совпадении размера и времени со снимком.
        /// </summary>
        public void DeleteUnchanged(string root, FileSnapshot file)
        {
            string path = SafeFilePath.Resolve(root, file.RelativePath);
            FileInfo current = new(path);
            if (!current.Exists || current.Length != file.Size || current.LastWriteTimeUtc != file.LastWriteTimeUtc)
            {
                throw new IOException("Файл изменился или исчез после сравнения. Повторите сравнение.");
            }
            SafeFilePath.Resolve(root, file.RelativePath);
            File.Delete(path);
        }
    }
}
