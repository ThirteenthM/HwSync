using HwSync.Abstractions.Models;
namespace HwSync.Abstractions.FileSystem
{
    public interface IFolderHistory
    {
        void RecordSnapshot(string rootPath, IReadOnlyCollection<FileSnapshot> snapshot);
        IReadOnlyList<DeletedFile> GetDeletedFiles(string rootPath);
    }
}
