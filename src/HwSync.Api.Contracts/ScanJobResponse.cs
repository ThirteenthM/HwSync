namespace HwSync.Api.Contracts
{
    public sealed record ScanJobResponse(Guid Id, ScanJobState Status, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt, IReadOnlyCollection<FileChangeDto>? Changes, string? Error);
}
