using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.FileSystem
{
    /// <summary>Хранение подтверждённого состояния клиента и папки.</summary>
    public interface IFolderSyncStateStore
    {
        /// <summary>Читает подтверждённое состояние клиента и папки, если оно существует.</summary>
        FolderSyncState? Load(Guid clientId, string folderId);

        /// <summary>Сохраняет подтверждённые версии файлов атомарной заменой состояния.</summary>
        void Save(FolderSyncState acknowledgedState);
    }
}
