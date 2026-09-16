using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.FileSystem
{
    public interface IFolderSyncStateStore
    {
        FolderSyncState? Load(Guid clientId, string folderId);
        void Save(FolderSyncState acknowledgedState);
    }
}
