namespace HwSync.Abstractions.Models
{
    public enum ContentConflictPolicy { KeepBoth }
    public enum DeletionConflictPolicy { AskUser }

    public sealed record ReconciliationRules
    {
        public ContentConflictPolicy ContentConflict { get; init; } = ContentConflictPolicy.KeepBoth;
        public DeletionConflictPolicy DeletionConflict { get; init; } = DeletionConflictPolicy.AskUser;
    }

    // Null content denotes an explicit tombstone, not an unobserved file.
    public sealed record FileVersion(string RelativePath, long Revision, string? ContentHash);

    // Contains only versions acknowledged after successful synchronization.
    public sealed record FolderSyncState(Guid ClientId, string FolderId, IReadOnlyList<FileVersion> Files);

    public enum ReconciliationAction
    {
        None, Upload, Download, DeleteOnServer, DeleteOnClient, KeepBoth, NeedsDecision
    }

    public sealed record ReconciliationItem(string RelativePath, ReconciliationAction Action,
        FileVersion? Baseline, FileVersion? Server, FileVersion? Client);

    public sealed record ReconciliationPlan(IReadOnlyList<ReconciliationItem> Items);
}
