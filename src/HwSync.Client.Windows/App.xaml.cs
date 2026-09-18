using System.IO;
using System.Text.Json;
using HwSync.Client.Windows.Configuration;
using System.Net.Http;
using System.Windows;
using HwSync.Api.Client;
using HwSync.Client.Windows.ViewModels;

namespace HwSync.Client.Windows
{
    /// <summary>
    /// Запуск WPF-клиента и управление временем жизни его зависимостей.
    /// </summary>
    public partial class App : Application
    {
        private readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        private readonly HttpClient _transferClient = new()
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        private MainViewModel? _viewModel;

        /// <summary>
        /// Читает настройки и открывает главное окно.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ClientSettings settings;
            IReadOnlyList<SyncProfile> profiles;
            string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            try
            {
                settings = ClientSettingsReader.Load(configPath, ClientSettingsReader.GetOptions());
                configPath = Path.Combine(AppContext.BaseDirectory, "sync-profiles.json");
                profiles = SyncProfileReader.Load(configPath, settings);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            {
                MessageBox.Show($"Не удалось прочитать настройки клиента из {configPath}.\n{exception.Message}",
                    "Ошибка конфигурации", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }
            _viewModel = new(address => new HwSyncApiClient(_httpClient, address, _transferClient, TimeSpan.FromSeconds(settings.FileTransferTimeoutSeconds)), new HwSync.Infrastructure.FileSystem.DirectorySnapshotProvider())
            {
                TransferMetricsEnabled = settings.TransferMetricsEnabled,
                ServerAddress = settings.ServerAddress,
                RootPath = settings.ServerRootPath,
                ClientRootPath = settings.ClientRootPath
            };
            _viewModel.LoadProfiles(profiles);
            MainWindow window = new(_viewModel);
            MainWindow = window;
            window.Show();
        }

        /// <summary>
        /// Отменяет текущую работу и освобождает HTTP-клиенты.
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            _viewModel?.Dispose();
            _httpClient.Dispose();
            _transferClient.Dispose();
            base.OnExit(e);
        }
    }
}
