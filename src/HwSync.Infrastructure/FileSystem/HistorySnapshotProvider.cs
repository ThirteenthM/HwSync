using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Infrastructure.FileSystem
{
    /// <summary>
    /// Запись истории только после полного успешного чтения клиентской папки.
    /// </summary>
    public sealed class HistorySnapshotProvider : IFileSnapshotProvider
    {
        private readonly IFileSnapshotProvider _source;
        private readonly IFolderHistory _history;

        /// <summary>
        /// Принимает чтение папки и хранилище её истории.
        /// </summary>
        public HistorySnapshotProvider(IFileSnapshotProvider source, IFolderHistory history)
        {
            _source = source;
            _history = history;
        }

        /// <summary>
        /// Читает полный снимок и фиксирует обнаруженные изменения.
        /// </summary>
        public IReadOnlyCollection<FileSnapshot> GetSnapshot(string rootPath)
        {
            IReadOnlyCollection<FileSnapshot> snapshot = _source.GetSnapshot(rootPath);
            _history.RecordSnapshot(rootPath, snapshot);
            return snapshot;
        }
    }
}
