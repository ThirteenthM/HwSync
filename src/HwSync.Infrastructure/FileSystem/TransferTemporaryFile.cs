namespace HwSync.Infrastructure.FileSystem;

/// <summary>
/// Имена служебных файлов незавершённой передачи.
/// </summary>
internal static class TransferTemporaryFile
{
    private const string Prefix = ".hwsync-";
    private const string Extension = ".tmp";
    private const int IdentifierLength = 32;

    /// <summary>
    /// Создаёт уникальный путь рядом с целевым файлом для последующего перемещения.
    /// </summary>
    public static string CreatePath(string directory) =>
        Path.Combine(directory, Prefix + Guid.NewGuid().ToString("N") + Extension);

    /// <summary>
    /// Распознаёт только зарезервированный формат имени временного файла HwSync.
    /// </summary>
    public static bool IsFileName(string name) =>
        name.Length == Prefix.Length + IdentifierLength + Extension.Length
        && name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
        && name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
        && Guid.TryParseExact(name.AsSpan(Prefix.Length, IdentifierLength), "N", out _);
}
