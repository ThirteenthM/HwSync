namespace HwSync.Api.Contracts
{
    public sealed record DeletedFileDto(string RelativePath, bool Deleted, DateTimeOffset DeletedAtUtc, long ChangeNumber);
}
