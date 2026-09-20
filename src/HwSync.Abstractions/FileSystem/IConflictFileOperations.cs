using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.FileSystem
{
    /// <summary>
    /// Замена конфликтующей версии с проверкой исходного снимка.
    /// </summary>
    public interface IConflictFileOperations
    {
        /// <summary>
        /// Получает новую версию во временный файл и заменяет неизменившийся оригинал.
        /// </summary>
        Task ReplaceAsync(string root, FileSnapshot expected, FileSnapshot incoming,
            Func<Stream, CancellationToken, Task> receive, CancellationToken token);
    }
}
