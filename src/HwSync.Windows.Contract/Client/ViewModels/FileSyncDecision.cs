namespace HwSync.Windows.Contract.Client.ViewModels
{
    /// <summary>
    /// Решение для одной строки сравнения.
    /// </summary>
    public enum FileSyncDecision
    {
        AskUser,
        Skip,
        CopyToServer,
        CopyToClient,
        DeleteOnServer,
        DeleteOnClient,
        ReplaceOnClient,
        ReplaceOnServer,
        KeepBoth
    }
}
