namespace HwSync.Api.Contracts
{
    /// <summary>Состояние и результат задания в контракте API.</summary>
    public sealed record ScanJobResponse(Guid Id, ScanJobState Status, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt, IReadOnlyCollection<FileChangeDto>? Changes, string? Error);
}
