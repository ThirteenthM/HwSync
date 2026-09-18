namespace HwSync.Abstractions.Models
{
    /// <summary>
    /// Действие плана двустороннего согласования.
    /// </summary>
    public enum ReconciliationAction
    {
        None,
        Upload,
        Download,
        DeleteOnServer,
        DeleteOnClient,
        KeepBoth,
        NeedsDecision
    }
}
