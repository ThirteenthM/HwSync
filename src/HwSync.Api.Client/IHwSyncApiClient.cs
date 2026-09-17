using HwSync.Api.Contracts;

namespace HwSync.Api.Client
{
    /// <summary>Запуск, опрос и отмена заданий сравнения через HTTP.</summary>
    public interface IHwSyncApiClient
    {
        /// <summary>Запрашивает готовность сервера.</summary>
        Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);

        /// <summary>Отправляет снимок клиента и запускает сравнение на сервере.</summary>
        Task<ScanJobResponse> StartComparisonAsync(CompareFoldersRequest request, CancellationToken cancellationToken = default);

        /// <summary>Получает состояние и результат задания.</summary>
        Task<ScanJobResponse> GetScanAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Запрашивает отмену задания на сервере.</summary>
        Task<ScanJobResponse> CancelScanAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
