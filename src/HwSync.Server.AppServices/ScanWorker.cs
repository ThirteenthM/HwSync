using HwSync.Abstractions.Models;
using HwSync.Abstractions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HwSync.Server.AppServices
{
    /// <summary>
    /// Запускает обработку очереди сканирования вместе с хостом.
    /// </summary>
    internal sealed class ScanWorker : BackgroundService
    {
        private readonly IScanJobRunner _runner;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ScanWorker> _logger;

        /// <summary>
        /// Принимает обработчик очереди, фабрику областей DI и журнал.
        /// </summary>
        public ScanWorker(IScanJobRunner runner, IServiceScopeFactory scopeFactory, ILogger<ScanWorker> logger)
        {
            _runner = runner;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// Ожидает обработку очереди до остановки хоста.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _runner.RunAsync(Scan, stoppingToken);
        }

        /// <summary>
        /// Выполняет одно сканирование в отдельной области зависимостей.
        /// </summary>
        private IReadOnlyCollection<FileChange> Scan(ChangeScanRequest request, CancellationToken cancellationToken)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IChangeScanner scanner = scope.ServiceProvider.GetRequiredService<IChangeScanner>();
                return scanner.Scan(request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Ошибка сканирования папки {RootPath}.", request.RootPath);
                throw;
            }
        }
    }
}