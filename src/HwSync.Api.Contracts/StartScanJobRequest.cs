namespace HwSync.Api.Contracts
{
    public sealed record StartScanJobRequest(string RootPath, IReadOnlyCollection<FileSnapshotDto> PreviousSnapshot);
}
