using System.IO;
using System.Text.Json;
using HwSync.Client.Windows.Application;
using HwSync.Client.Windows.Contract.Configuration;
using HwSync.Client.Windows.Contract.Services;
using HwSync.Client.Windows.Contract.ViewModels;
using HwSync.Client.Windows.Host;
using Microsoft.Extensions.DependencyInjection;

namespace HwSync.Client.Tests
{
    /// <summary>
    /// Проверки сборки Windows-клиента из контрактов и реализаций через DI.
    /// </summary>
    public class ClientDependencyInjectionTests
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
                ServerAddress = "http://localhost:9010",
                ServerRootPath = Path.Combine(directory, "server"),
                ClientRootPath = Path.Combine(directory, "client"),
                TransferMetricsEnabled = false
            }));
            IServiceCollection services = new ServiceCollection();
            services.AddWindowsClientApplication(directory);
            services.AddTransient<MainWindow>();
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            MainWindow window = provider.GetRequiredService<MainWindow>();
            try
            {
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
        /// Проверяет запуск с начальными настройками при отсутствии файлов.
        /// </summary>
        [Test]
        public void Container_UsesDefaultsWithoutConfigurationFiles()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddWindowsClientApplication(CreateDirectory());
            using ServiceProvider provider = services.BuildServiceProvider();
            IMainViewModel model = provider.GetRequiredService<IMainViewModel>();
            Assert.That(model.Profiles.Count, Is.EqualTo(1));
            Assert.That(model.ClientRootPath, Is.Empty);
            Assert.That(model.StartCommand.CanExecute(null), Is.False);
        }

        /// <summary>
        /// Проверяет сохранение пути повреждённой конфигурации в ошибке запуска.
        /// </summary>
        [Test]
        public void Container_ReportsInvalidConfigurationPath()
        {
            string directory = CreateDirectory();
            string path = Path.Combine(directory, "sync-profiles.json");
            File.WriteAllText(path, "null");
            IServiceCollection services = new ServiceCollection();
            services.AddWindowsClientApplication(directory);
            using ServiceProvider provider = services.BuildServiceProvider();
            InvalidDataException? error = Assert.Throws<InvalidDataException>(() => provider.GetRequiredService<IMainViewModel>());
            Assert.That(error!.Message, Does.Contain(path));
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
