namespace HwSync.Abstractions.Administration
{
    /// <summary>
    /// Запись удаления с источником и последней известной версией.
    /// </summary>
    public sealed record DeletionEntry(
        long Number, string RelativePath, string OriginParticipantId,
        DateTimeOffset DeletedAtUtc, long PreviousSize, DateTimeOffset PreviousModifiedUtc, bool Active);
}
