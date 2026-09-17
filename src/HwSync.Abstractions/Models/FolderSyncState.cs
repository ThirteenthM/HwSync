namespace HwSync.Abstractions.Models
{
    // Contains only versions acknowledged after successful synchronization.
    /// <summary>Подтверждённые версии файлов конкретного клиента и папки.</summary>
    public sealed record FolderSyncState(Guid ClientId, string FolderId, IReadOnlyList<FileVersion> Files);
}
