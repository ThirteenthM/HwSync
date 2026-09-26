using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Windows.Client.Application.Tests
{
    /// <summary>
    /// Поставщик снимка для тестов модели формы.
    /// </summary>
    internal sealed class StubSnapshotProvider : IFileSnapshotProvider
    {
        /// <summary>
        /// Возвращает снимок файлов относительно корневой папки.
        /// </summary>
        public IReadOnlyCollection<FileSnapshot> GetSnapshot(string rootPath, CancellationToken cancellationToken = default) =>
            [new("client.txt", 12, new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc))];
    }
}
