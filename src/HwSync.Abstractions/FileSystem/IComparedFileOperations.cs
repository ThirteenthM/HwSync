using HwSync.Abstractions.Models;
namespace HwSync.Abstractions.FileSystem
{
    /// <summary>
    /// Файловые операции с проверкой состояния из сравнения.
    /// </summary>
    public interface IComparedFileOperations
    {
        /// <summary>
        /// Копирует отсутствующий файл без перезаписи существующего.
        /// </summary>
        Task CopyMissingAsync(string root, FileSnapshot file, Stream content, CancellationToken token);

        /// <summary>
        /// Удаляет файл только при совпадении размера и времени со снимком.
        /// </summary>
        void DeleteUnchanged(string root, FileSnapshot file);

        /// <summary>
        /// Проверяет отсутствие пути и доступность корневой папки.
        /// </summary>
        void EnsureMissing(string root, string relativePath);
    }
}
