namespace HwSync.Api.Contracts.Administration
{
    /// <summary>
    /// Известная серверу папка и показатели последнего снимка.
    /// </summary>
    public sealed record ServerFolderDto(string Id, string RootPath, long FileCount, long ActiveDeletionCount);
}
