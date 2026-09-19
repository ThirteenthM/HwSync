using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.FileSystem
{
    /// <summary>
    /// Копирование отсутствующих файлов с сохранением существующих.
    /// </summary>
    public interface IMissingFileSynchronizer
    {
        /// <summary>
        /// Копирует файлы через заданный способ получения содержимого.
        /// </summary>
        Task<IReadOnlyList<FileCopyResult>> CopyAsync(string root, IReadOnlyCollection<FileSnapshot> files,
            Func<FileSnapshot, Stream, CancellationToken, Task> download, CancellationToken cancellationToken);
    }
}
