namespace HwSync.Api.Contracts
{
    /// <summary>Относительный путь, размер и время файла в контракте API.</summary>
    public sealed record FileSnapshotDto(string RelativePath, long Size, DateTime LastWriteTimeUtc);
}
