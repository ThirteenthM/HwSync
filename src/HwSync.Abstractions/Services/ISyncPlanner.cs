using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.Services
{
    public interface ISyncPlanner
    {
        SyncPlan Create(IReadOnlyCollection<FileChange> changes, SyncMode mode);
    }
}
