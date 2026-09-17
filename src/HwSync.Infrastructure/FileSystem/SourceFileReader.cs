using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
namespace HwSync.Infrastructure.FileSystem
{
    /// <summary>Чтение исходного файла с проверкой атрибутов сравнения.</summary>
    public sealed class SourceFileReader : ISourceFileReader
    {
        /// <summary>Открывает файл для чтения, проверяя путь, размер и время изменения.</summary>
        public Stream OpenRead(string rootPath, FileSnapshot expected)
        {
            string path = SafeFilePath.Resolve(rootPath, expected.RelativePath);
            FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            try
            {
                if (stream.Length != expected.Size || File.GetLastWriteTimeUtc(path) != expected.LastWriteTimeUtc)
                {
                    throw new IOException("Файл сервера изменился после сравнения. Выполните сравнение повторно.");
                }
                return stream;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
    }
}
