namespace HwSync.Api.Contracts.Administration
{
    /// <summary>
    /// Страница журнала и номер для продолжения чтения.
    /// </summary>
    public sealed record DeletionPageDto(IReadOnlyList<DeletionEntryDto> Entries, long? NextCursor);
}
