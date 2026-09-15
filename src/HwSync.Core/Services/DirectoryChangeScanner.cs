using HwSync.Abstractions.Services;
using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Core.Services
{
    public sealed class DirectoryChangeScanner : IChangeScanner
    {
        private readonly IFileSnapshotProvider _snapshotProvider;
        private readonly IChangeComparer _changeComparer;

        public DirectoryChangeScanner(
            IFileSnapshotProvider snapshotProvider,
            IChangeComparer changeComparer)
        {
            _snapshotProvider = snapshotProvider;
            _changeComparer = changeComparer;
        }

        public IReadOnlyCollection<FileChange> Scan(ChangeScanRequest request)
        {
            IReadOnlyCollection<FileSnapshot> currentSnapshot =
                _snapshotProvider.GetSnapshot(request.RootPath);

            return _changeComparer.Compare(request.PreviousSnapshot, currentSnapshot);
        }
    }
}
