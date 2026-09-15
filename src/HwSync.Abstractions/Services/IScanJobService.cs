using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.Services
{
    public interface IScanJobService
    {
        ScanJob Start(ChangeScanRequest request);
        ScanJob? Get(Guid id);
        ScanJob? Cancel(Guid id);
    }
}
