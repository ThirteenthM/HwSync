using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
namespace HwSync.Infrastructure.FileSystem
{
    public sealed class SourceFileReader : ISourceFileReader
    {
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
            catch { stream.Dispose(); throw; }
        }
    }
}
