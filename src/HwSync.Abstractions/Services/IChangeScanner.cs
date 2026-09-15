using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.Services
{
    public interface IChangeScanner
    {
        IReadOnlyCollection<FileChange> Scan(ChangeScanRequest request);
    }
}
