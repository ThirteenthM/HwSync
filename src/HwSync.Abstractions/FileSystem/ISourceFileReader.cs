using HwSync.Abstractions.Models;
namespace HwSync.Abstractions.FileSystem
{
    /// <summary>
    /// Открытие исходного файла с проверкой снимка.
    /// </summary>
    public interface ISourceFileReader
    {
        /// <summary>
        /// Открывает файл для чтения, проверяя путь, размер и время изменения.
        /// </summary>
        Stream OpenRead(string rootPath, FileSnapshot expected);
    }
}
