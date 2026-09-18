namespace HwSync.Api.Contracts
{
    /// <summary>
    /// Путь серверной папки и снимок клиента для сравнения.
    /// </summary>
    public sealed record CompareFoldersRequest(string ServerRootPath, IReadOnlyCollection<FileSnapshotDto> ClientSnapshot);
}
