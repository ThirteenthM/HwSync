namespace HwSync.Windows.Contract.Client.ViewModels
{
    /// <summary>
    /// Причина разбирательства различающихся состояний файла.
    /// </summary>
    public enum FileConflictKind
    {
        Content,
        DeletedOnClient,
        DeletedOnServer
    }
}
