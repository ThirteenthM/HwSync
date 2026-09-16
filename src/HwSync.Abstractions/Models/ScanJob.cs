namespace HwSync.Abstractions.Models
{
    public enum ScanJobStatus { Queued, Running, CancellationRequested, Completed, Cancelled, Failed }

    public sealed record ScanJob(
        Guid Id,
        ScanJobStatus Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? FinishedAt,
        IReadOnlyCollection<FileChange>? Changes,
        string? Error, string? SourceRootPath = null);
}
