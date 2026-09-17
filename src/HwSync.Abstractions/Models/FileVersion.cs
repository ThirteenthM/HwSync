namespace HwSync.Abstractions.Models
{
    // Null content denotes an explicit tombstone, not an unobserved file.
    /// <summary>Версия содержимого; отсутствие хеша обозначает явное удаление.</summary>
    public sealed record FileVersion(string RelativePath, long Revision, string? ContentHash);
}
