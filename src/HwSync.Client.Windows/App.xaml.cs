using System.IO;
using System.Text.Json;
using HwSync.Client.Windows.Configuration;
using System.Net.Http;
using System.Windows;
using HwSync.Api.Client;
using HwSync.Client.Windows.ViewModels;

namespace HwSync.Client.Windows
{
    public partial class App : Application
    {
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
        private MainViewModel? _viewModel;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ClientSettings settings;
            IReadOnlyList<SyncProfile> profiles;
            string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            try
            {
                settings = ClientSettingsReader.Load(configPath);
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
            _viewModel = new(address => new HwSyncApiClient(_httpClient, address), new HwSync.Infrastructure.FileSystem.DirectorySnapshotProvider())
            {
                ServerAddress = settings.ServerAddress,
                RootPath = settings.ServerRootPath,
                ClientRootPath = settings.ClientRootPath
            };
            _viewModel.LoadProfiles(profiles);
            MainWindow window = new(_viewModel);
            MainWindow = window;
            window.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _viewModel?.Dispose();
            _httpClient.Dispose();
            base.OnExit(e);
        }
    }
}
