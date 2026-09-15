namespace HwSync.Api.Contracts
{
    public sealed record FileSnapshotDto(string RelativePath, long Size, DateTime LastWriteTimeUtc);
}
