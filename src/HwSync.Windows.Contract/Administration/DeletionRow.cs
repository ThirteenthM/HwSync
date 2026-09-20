namespace HwSync.Windows.Contract.Administration
{
    /// <summary>
    /// Строка похоронной книги с размером в десятичных мегабайтах.
    /// </summary>
    public sealed record DeletionRow(
        long Number, string Path, string OriginParticipantId, DateTime DeletedAtUtc,
        decimal SizeMb, DateTime ModifiedUtc, string State);
}
