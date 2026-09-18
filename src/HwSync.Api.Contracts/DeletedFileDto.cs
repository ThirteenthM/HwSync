namespace HwSync.Api.Contracts
{
    /// <summary>
    /// Передаваемая через API отметка удаления файла.
    /// </summary>
    public sealed record DeletedFileDto(string RelativePath, bool Deleted, DateTimeOffset DeletedAtUtc, long ChangeNumber);
}
