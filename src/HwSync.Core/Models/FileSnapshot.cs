namespace HwSync.Core.Models
{
    public sealed record FileSnapshot(
        string RelativePath,
        long Size,
        DateTime LastWriteTimeUtc
    );
}
