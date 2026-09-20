using System.IO;
using HwSync.Windows.AppServices.Client;
using HwSync.Windows.Contract.Client.Configuration;
using HwSync.Windows.Contract.Client.Services;
using HwSync.Windows.Contract.Client.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HwSync.Client.Tests
{
    /// <summary>
    /// Проверки сборки Windows-клиента из контрактов и реализаций через DI.
    /// </summary>
    public class ClientDependencyInjectionTests
    {
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
            Assert.That(model.Profiles, Has.Count.EqualTo(1));
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
