namespace HwSync.Abstractions.Models
{
    /// <summary>Состояние серверного задания и его результат.</summary>
    public sealed record ScanJob(Guid Id, ScanJobStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt, IReadOnlyCollection<FileChange>? Changes, string? Error, string? SourceRootPath = null);
}
