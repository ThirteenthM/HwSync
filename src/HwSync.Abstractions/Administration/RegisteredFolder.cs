namespace HwSync.Abstractions.Administration
{
    /// <summary>
    /// Папка, известная базе после успешного сканирования.
    /// </summary>
    public sealed record RegisteredFolder(string Id, string RootPath, long FileCount, long ActiveDeletionCount);
}
