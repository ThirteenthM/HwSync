namespace HwSync.Abstractions.Models
{
    /// <summary>Относительный путь, размер и время изменения файла.</summary>
    public sealed record FileSnapshot(
        string RelativePath,
        long Size,
        DateTime LastWriteTimeUtc
    );
}
