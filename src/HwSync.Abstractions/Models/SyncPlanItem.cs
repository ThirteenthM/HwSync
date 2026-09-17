namespace HwSync.Abstractions.Models
{
    /// <summary>Действие над файлом и снимки обеих сторон.</summary>
    public sealed record SyncPlanItem(SyncAction Action, string RelativePath, FileSnapshot? ClientFile, FileSnapshot? ServerFile);
}
