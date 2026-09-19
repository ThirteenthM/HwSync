using System.Text.Json;
using HwSync.Abstractions.FileSystem;
using HwSync.Api.Client;
using HwSync.Client.Windows.Application.Configuration;
using HwSync.Client.Windows.Application.ViewModels;
using HwSync.Client.Windows.Contract.Configuration;
using HwSync.Client.Windows.Contract.Services;
using HwSync.Client.Windows.Contract.ViewModels;
using HwSync.Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;

namespace HwSync.Client.Windows.Application
{
    /// <summary>
    /// Регистрация реализаций Windows-клиента и их времени жизни.
    /// </summary>
    public static class WindowsClientServices
    {
        /// <summary>
        /// Подключает службы клиента с конфигурацией из заданного каталога.
        /// </summary>
        public static IServiceCollection AddWindowsClientApplication(this IServiceCollection services, string configurationDirectory)
        {
            string directory = Path.GetFullPath(configurationDirectory);
            services.AddSingleton<IConfigurationReader<ClientSettings>, ClientSettingsReader>();
            services.AddSingleton<ClientSettings>(provider => LoadConfiguration(
                provider.GetRequiredService<IConfigurationReader<ClientSettings>>(), Path.Combine(directory, "appsettings.json")));
            services.AddSingleton<IConfigurationReader<List<SyncProfile>>, SyncProfileReader>();
            services.AddSingleton<List<SyncProfile>>(provider => LoadConfiguration(
                provider.GetRequiredService<IConfigurationReader<List<SyncProfile>>>(), Path.Combine(directory, "sync-profiles.json")));
            services.AddSingleton<ClientHttpClients>();
            services.AddSingleton<Func<Uri, IHwSyncApiClient>>(provider => provider.GetRequiredService<ClientHttpClients>().Create);
            services.AddSingleton<IFileSnapshotProvider, DirectorySnapshotProvider>();
            services.AddSingleton<IComparedFileOperations, ComparedFileOperations>();
            services.AddSingleton<ISourceFileReader, SourceFileReader>();
            services.AddSingleton<IMissingFileSynchronizer, MissingFileSynchronizer>();
            services.AddSingleton<ISyncDecisionService, SyncDecisionService>();
            services.AddSingleton<IMainViewModel>(provider =>
            {
                ClientSettings settings = provider.GetRequiredService<ClientSettings>();
                List<SyncProfile> profiles = provider.GetRequiredService<List<SyncProfile>>();
                MainViewModel model = new(
                    provider.GetRequiredService<Func<Uri, IHwSyncApiClient>>(),
                    provider.GetRequiredService<IFileSnapshotProvider>(),
                    provider.GetRequiredService<IComparedFileOperations>(),
                    provider.GetRequiredService<ISourceFileReader>(),
                    provider.GetRequiredService<IMissingFileSynchronizer>(),
                    provider.GetRequiredService<ISyncDecisionService>())
                {
                    TransferMetricsEnabled = settings.TransferMetricsEnabled,
                    ServerAddress = settings.ServerAddress,
                    RootPath = settings.ServerRootPath,
                    ClientRootPath = settings.ClientRootPath
                };
                model.LoadProfiles(profiles);
                return model;
            });
            return services;
        }

        /// <summary>
        /// Добавляет путь к сообщению об ошибке чтения конфигурации.
        /// </summary>
        private static T LoadConfiguration<T>(IConfigurationReader<T> reader, string path) where T : class
        {
            try
            {
                return reader.Load(path);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            {
                throw new InvalidDataException($"Не удалось прочитать настройки из {path}.{Environment.NewLine}{exception.Message}", exception);
            }
        }
    }
}
