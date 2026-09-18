using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Core.Tests.Services
{
    /// <summary>
    /// Проверки взаимодействия сканера с поставщиком снимков.
    /// </summary>
    public partial class DirectoryChangeScannerTests
    {
        private static readonly DateTime LastWriteTimeUtc =
            new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Подставной поставщик снимков с учётом обращений.
        /// </summary>
        private sealed class StubFileSnapshotProvider : IFileSnapshotProvider
        {
            public IReadOnlyCollection<FileSnapshot> Snapshot
            {
                get;
                set;
            }

            public List<string> RequestedRootPaths
            {
                get;
            } = [];

            /// <summary>
            /// Задаёт снимки для последовательных обращений теста.
            /// </summary>
            public StubFileSnapshotProvider(IReadOnlyCollection<FileSnapshot> snapshot)
            {
                Snapshot = snapshot;
            }

            /// <summary>
            /// Возвращает снимок файлов относительно корневой папки.
            /// </summary>
            public IReadOnlyCollection<FileSnapshot> GetSnapshot(string rootPath)
            {
                RequestedRootPaths.Add(rootPath);
                return Snapshot;
            }
        }
    }
}
