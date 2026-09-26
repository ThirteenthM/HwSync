using HwSync.Abstractions.Services;
using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Core.Services
{
    /// <summary>
    /// Сравнение свежего снимка папки с предыдущим.
    /// </summary>
    public sealed class DirectoryChangeScanner : IChangeScanner
    {
        private readonly IFileSnapshotProvider _snapshotProvider;
        private readonly IChangeComparer _changeComparer;
        private readonly IFolderHistory? _history;

        /// <summary>
        /// Принимает чтение снимков, компаратор и необязательную историю.
        /// </summary>
        public DirectoryChangeScanner(
            IFileSnapshotProvider snapshotProvider,
            IChangeComparer changeComparer, IFolderHistory? history = null)
        {
            _snapshotProvider = snapshotProvider;
            _changeComparer = changeComparer;
            _history = history;
        }

        /// <summary>
        /// Читает папку, сравнивает снимки и обновляет историю.
        /// </summary>
        public IReadOnlyCollection<FileChange> Scan(ChangeScanRequest request, CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<FileSnapshot> currentSnapshot =
                _snapshotProvider.GetSnapshot(request.RootPath, cancellationToken);

            IReadOnlyCollection<FileChange> changes = _changeComparer.Compare(request.PreviousSnapshot, currentSnapshot);
            cancellationToken.ThrowIfCancellationRequested();
            _history?.RecordSnapshot(request.RootPath, currentSnapshot);
            return changes;
        }
    }
}
