namespace HwSync.Abstractions.Models
{
    public sealed record DeletedFile(string RelativePath, bool Deleted, DateTimeOffset DeletedAtUtc, long ChangeNumber, FileSnapshot PreviousFile);
}
