namespace HwSync.Abstractions.Models
{
    /// <summary>
    /// Односторонний план синхронизации выбранного режима.
    /// </summary>
    public sealed record SyncPlan(SyncMode Mode, IReadOnlyList<SyncPlanItem> Items);
}
