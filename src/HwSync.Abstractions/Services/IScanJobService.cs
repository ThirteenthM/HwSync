using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.Services
{
    /// <summary>Запуск, получение состояния и отмена серверных заданий.</summary>
    public interface IScanJobService
    {
        /// <summary>Ставит сравнение папки в очередь.</summary>
        ScanJob Start(ChangeScanRequest request);

        /// <summary>Возвращает состояние задания по идентификатору.</summary>
        ScanJob? Get(Guid id);

        /// <summary>Запрашивает отмену задания по идентификатору.</summary>
        ScanJob? Cancel(Guid id);
    }
}
