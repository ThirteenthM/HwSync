using HwSync.Abstractions.Models;

namespace HwSync.Infrastructure.FileSystem
{
    /// <summary>
    /// Результат копирования одного файла и причина отказа.
    /// </summary>
    public sealed record FileCopyResult(string RelativePath, bool Copied, string? Error);
}
