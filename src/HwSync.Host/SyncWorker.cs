using HwSync.Abstractions.Services;
using HwSync.Core.Services;

namespace HwSync.Host
{
    internal sealed class SyncWorker : BackgroundService
    {
        private readonly ScanJobService _jobs;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SyncWorker> _logger;

        public SyncWorker(ScanJobService jobs, IServiceScopeFactory scopeFactory, ILogger<SyncWorker> logger)
        {
            _jobs = jobs;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return _jobs.RunAsync(request =>
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                try
                {
                    return scope.ServiceProvider.GetRequiredService<IChangeScanner>().Scan(request);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Ошибка выполнения задания сканирования.");
                    throw;
                }
            }, stoppingToken);
        }
    }
}
