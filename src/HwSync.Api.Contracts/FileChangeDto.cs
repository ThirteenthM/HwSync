namespace HwSync.Api.Contracts
{
    public sealed record FileChangeDto(FileChangeKind ChangeType, FileSnapshotDto? Previous, FileSnapshotDto? Current);
}
