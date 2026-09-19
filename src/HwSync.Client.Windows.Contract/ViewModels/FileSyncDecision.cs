namespace HwSync.Client.Windows.Contract.ViewModels
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
        DeleteOnClient
    }
}