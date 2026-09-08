using HwSync.Core.Models;

namespace HwSync.Core.Services
{
    public interface IChangeScanner
    {
        IReadOnlyCollection<FileChange> Scan(ChangeScanRequest request);
    }
}
