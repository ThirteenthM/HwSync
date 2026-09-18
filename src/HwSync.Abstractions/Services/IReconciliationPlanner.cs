using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.Services
{
    /// <summary>
    /// Построение плана по общему состоянию и снимкам сторон.
    /// </summary>
    public interface IReconciliationPlanner
    {
        /// <summary>
        /// Согласует снимки сторон с последним подтверждённым состоянием.
        /// </summary>
        ReconciliationPlan Create(FolderSyncState baseline, IReadOnlyCollection<FileVersion> server,
            IReadOnlyCollection<FileVersion> client, ReconciliationRules rules);
    }
}
