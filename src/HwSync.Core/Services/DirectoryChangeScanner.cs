using HwSync.Abstractions.Services;
using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Core.Services
{
    public sealed class DirectoryChangeScanner : IChangeScanner
    {
        private readonly IFileSnapshotProvider _snapshotProvider;
        private readonly IChangeComparer _changeComparer;
        private readonly IFolderHistory? _history;

        public DirectoryChangeScanner(
            IFileSnapshotProvider snapshotProvider,
            IChangeComparer changeComparer, IFolderHistory? history = null)
        {
            _snapshotProvider = snapshotProvider;
            _changeComparer = changeComparer;
            _history = history;
        }

        public IReadOnlyCollection<FileChange> Scan(ChangeScanRequest request)
        {
            IReadOnlyCollection<FileSnapshot> currentSnapshot =
                _snapshotProvider.GetSnapshot(request.RootPath);

            IReadOnlyCollection<FileChange> changes = _changeComparer.Compare(request.PreviousSnapshot, currentSnapshot);
            _history?.RecordSnapshot(request.RootPath, currentSnapshot);
            return changes;
        }
    }
}
