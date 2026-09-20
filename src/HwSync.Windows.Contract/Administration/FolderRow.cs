namespace HwSync.Windows.Contract.Administration
{
    /// <summary>
    /// Папка сервера и количество записей в последнем сохранённом снимке.
    /// </summary>
    public sealed record FolderRow(string Id, string Path, long FileCount, long ActiveDeletionCount);
}
