using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
namespace HwSync.Infrastructure.FileSystem
{
    public sealed class ComparedFileOperations : IComparedFileOperations
    {
        public void EnsureMissing(string root, string relativePath)
        {
            if ((File.GetAttributes(root) & FileAttributes.Directory) == 0)
            { throw new IOException("Корневая папка недоступна."); }
            string path = SafeFilePath.Resolve(root, relativePath);
            try { File.GetAttributes(path); }
            catch (FileNotFoundException) { return; }
            catch (DirectoryNotFoundException) { return; }
            throw new IOException("Путь уже существует. Повторите сравнение.");
        }

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
                        if (read == 0) { throw new IOException("Передача файла прервана."); }
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
                        remaining -= read;
                    }
                    if (await content.ReadAsync(buffer.AsMemory(0, 1), cancellation) != 0)
                    { throw new IOException("Размер файла превышает снимок."); }
                }, token);
            if (!results[0].Copied) { throw new IOException(results[0].Error); }
        }

        public void DeleteUnchanged(string root, FileSnapshot file)
        {
            string path = SafeFilePath.Resolve(root, file.RelativePath);
            FileInfo current = new(path);
            if (!current.Exists || current.Length != file.Size || current.LastWriteTimeUtc != file.LastWriteTimeUtc)
            { throw new IOException("Файл изменился или исчез после сравнения. Повторите сравнение."); }
            SafeFilePath.Resolve(root, file.RelativePath);
            File.Delete(path);
        }
    }
}
