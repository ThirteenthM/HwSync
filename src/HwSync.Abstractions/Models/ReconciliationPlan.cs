namespace HwSync.Abstractions.Models
{
    /// <summary>План согласования без выполнения файловых операций.</summary>
    public sealed record ReconciliationPlan(IReadOnlyList<ReconciliationItem> Items);
}
