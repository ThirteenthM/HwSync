using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.Models
{
    /// <summary>
    /// Результат копирования одного файла и причина отказа.
    /// </summary>
    public sealed record FileCopyResult(string RelativePath, bool Copied, string? Error);
}
