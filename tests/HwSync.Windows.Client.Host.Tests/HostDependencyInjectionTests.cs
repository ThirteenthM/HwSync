using HwSync.Windows.Contract.Common.Services;
using System.IO;
using System.Text.Json;
using HwSync.Windows.AppServices.Client;
using HwSync.Windows.Contract.Client.Configuration;
using HwSync.Windows.Contract.Client.Services;
using HwSync.Windows.Contract.Client.ViewModels;
using HwSync.Windows.Client.Host;
using Microsoft.Extensions.DependencyInjection;

namespace HwSync.Windows.Client.Host.Tests
{
    /// <summary>
    /// Проверки сборки Windows-клиента из контрактов и реализаций через DI.
    /// </summary>
    public class HostDependencyInjectionTests
    {
        /// <summary>
        /// Проверяет создание окна через DI и применение настроек к модели.
        /// </summary>
        [Test]
        [Apartment(ApartmentState.STA)]
        public void Container_CreatesWindowAndLoadsConfiguration()
        {
            string directory = CreateDirectory();
            File.WriteAllText(Path.Combine(directory, "appsettings.json"), JsonSerializer.Serialize(new ClientSettings
            {
                DatabasePath = Path.Combine(directory, "metadata", "state.db"),
                ServerAddress = "http://localhost:9010",
                ServerRootPath = Path.Combine(directory, "server"),
                ClientRootPath = Path.Combine(directory, "client"),
                TransferMetricsEnabled = false
            }));
            IServiceCollection services = new ServiceCollection();
            services.AddWindowsClientApplication(directory);
            ClientStorageBootstrap.AddClientStorage(services);
            services.AddTransient<MainWindow>();
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            ClientStorageBootstrap.Initialize(provider);
            ClientStorageBootstrap.Initialize(provider);
            MainWindow window = provider.GetRequiredService<MainWindow>();
            try
            {
                string clientRoot = Path.Combine(directory, "client");
                Directory.CreateDirectory(clientRoot);
                string file = Path.Combine(clientRoot, "removed.txt");
                File.WriteAllText(file, "original");
                HwSync.Abstractions.FileSystem.IFileSnapshotProvider snapshots = provider.GetRequiredService<HwSync.Abstractions.FileSystem.IFileSnapshotProvider>();
                snapshots.GetSnapshot(clientRoot);
                File.Delete(file);
                snapshots.GetSnapshot(clientRoot);
                HwSync.Abstractions.FileSystem.IFolderHistory history = provider.GetRequiredService<HwSync.Abstractions.FileSystem.IFolderHistory>();
                Assert.That(history, Is.TypeOf<HwSync.Persistence.Sqlite.SqliteFolderHistory>());
                Assert.That(history.GetDeletedFiles(clientRoot).Single().RelativePath, Is.EqualTo("removed.txt"));
                IMainViewModel model = provider.GetRequiredService<IMainViewModel>();
                Assert.That(window.DataContext, Is.SameAs(model));
                Assert.That(model.ServerAddress, Is.EqualTo("http://localhost:9010"));
                Assert.That(model.SelectedProfile!.ClientRootPath, Is.EqualTo(Path.Combine(directory, "client")));
                Assert.That(provider.GetRequiredService<IMainViewModel>(), Is.SameAs(model));
                Assert.That(provider.GetRequiredService<IConfigurationReader<ClientSettings>>(), Is.Not.Null);
                Assert.That(provider.GetRequiredService<ClientSettings>().TransferMetricsEnabled, Is.False);
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>
        /// Повреждённая база прерывает инициализацию до создания окна.
        /// </summary>
        [Test]
        public void Storage_InvalidDatabaseStopsStartup()
        {
            string directory = CreateDirectory();
            string databasePath = Path.Combine(directory, "broken.db");
            File.WriteAllText(databasePath, "not a sqlite database");
            File.WriteAllText(Path.Combine(directory, "appsettings.json"),
                JsonSerializer.Serialize(new ClientSettings { DatabasePath = databasePath }));
            IServiceCollection services = new ServiceCollection();
            services.AddWindowsClientApplication(directory);
            ClientStorageBootstrap.AddClientStorage(services);
            using ServiceProvider provider = services.BuildServiceProvider();
            Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => ClientStorageBootstrap.Initialize(provider));
            Assert.That(File.ReadAllText(databasePath), Is.EqualTo("not a sqlite database"));
        }
        /// <summary>
        /// Создаёт изолированный каталог конфигурации для теста.
        /// </summary>
        private static string CreateDirectory()
        {
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "client-di", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
