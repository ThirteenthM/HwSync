using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.Services
{
    /// <summary>Построение одностороннего плана по различиям файлов.</summary>
    public interface ISyncPlanner
    {
        /// <summary>Преобразует различия в действия выбранного режима.</summary>
        SyncPlan Create(IReadOnlyCollection<FileChange> changes, SyncMode mode);
    }
}
