using HwSync.Abstractions.Models;
namespace HwSync.Abstractions.FileSystem
{
    /// <summary>Хранение серверных снимков и отметок удаления.</summary>
    public interface IFolderHistory
    {
        /// <summary>Сохраняет снимок и отмечает исчезнувшие файлы.</summary>
        void RecordSnapshot(string rootPath, IReadOnlyCollection<FileSnapshot> snapshot);

        /// <summary>Возвращает сохранённые отметки удаления.</summary>
        IReadOnlyList<DeletedFile> GetDeletedFiles(string rootPath);
    }
}
