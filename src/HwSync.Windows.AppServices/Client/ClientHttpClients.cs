using HwSync.Api.Client;
using HwSync.Windows.Contract.Client.Configuration;

namespace HwSync.Windows.AppServices.Client
{
    /// <summary>
    /// Общие HTTP-клиенты для запросов и передачи файлов.
    /// </summary>
    internal sealed class ClientHttpClients : IDisposable
    {
        private readonly HttpClient _requests = new() { Timeout = TimeSpan.FromSeconds(15) };
        private readonly HttpClient _transfers = new() { Timeout = Timeout.InfiniteTimeSpan };
        private readonly ClientSettings _settings;

        /// <summary>
        /// Сохраняет настройки ожидания прогресса передачи.
        /// </summary>
        public ClientHttpClients(ClientSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Создаёт API-клиент для выбранного адреса.
        /// </summary>
        public IHwSyncApiClient Create(Uri address) =>
            new HwSyncApiClient(_requests, address, _transfers, TimeSpan.FromSeconds(_settings.FileTransferTimeoutSeconds));

        /// <summary>
        /// Освобождает соединения при завершении приложения.
        /// </summary>
        public void Dispose()
        {
            _requests.Dispose();
            _transfers.Dispose();
        }
    }
}
