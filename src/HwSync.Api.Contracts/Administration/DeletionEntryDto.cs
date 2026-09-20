namespace HwSync.Api.Contracts.Administration
{
    /// <summary>
    /// Административное представление записи похоронной книги.
    /// </summary>
    public sealed record DeletionEntryDto(
        long Number, string RelativePath, string OriginParticipantId,
        DateTimeOffset DeletedAtUtc, long PreviousSize, DateTimeOffset PreviousModifiedUtc, bool Active);
}
