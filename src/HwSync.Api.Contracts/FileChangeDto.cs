namespace HwSync.Api.Contracts
{
    /// <summary>
    /// Различие между клиентским и серверным файлами.
    /// </summary>
    public sealed record FileChangeDto(FileChangeKind ChangeType, FileSnapshotDto? Previous, FileSnapshotDto? Current);
}
