using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.Services
{
    public interface IReconciliationPlanner
    {
        ReconciliationPlan Create(FolderSyncState baseline, IReadOnlyCollection<FileVersion> server,
            IReadOnlyCollection<FileVersion> client, ReconciliationRules rules);
    }
}
