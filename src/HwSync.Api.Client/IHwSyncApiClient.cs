using HwSync.Api.Contracts;

namespace HwSync.Api.Client
{
    public interface IHwSyncApiClient
    {
        Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);
        Task<ScanJobResponse> StartComparisonAsync(CompareFoldersRequest request, CancellationToken cancellationToken = default);
        Task<ScanJobResponse> GetScanAsync(Guid id, CancellationToken cancellationToken = default);
        Task<ScanJobResponse> CancelScanAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
