namespace HwSync.Api.Contracts
{
    public sealed record CompareFoldersRequest(string ServerRootPath, IReadOnlyCollection<FileSnapshotDto> ClientSnapshot);
}
