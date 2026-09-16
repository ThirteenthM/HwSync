namespace HwSync.Abstractions.Models
{
    public enum SyncMode { CopyMissing, Update, Mirror }
    public enum SyncAction { CopyToClient, ReplaceOnClient, KeepOnClient, DeleteFromClient }

    public sealed record SyncPlanItem(SyncAction Action, string RelativePath, FileSnapshot? ClientFile, FileSnapshot? ServerFile);
    public sealed record SyncPlan(SyncMode Mode, IReadOnlyList<SyncPlanItem> Items);
}
