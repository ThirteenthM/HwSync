namespace HwSync.Abstractions.Models
{
    /// <summary>
    /// Действие одностороннего плана.
    /// </summary>
    public enum SyncAction
    {
        CopyToClient,
        ReplaceOnClient,
        KeepOnClient,
        DeleteFromClient
    }
}
