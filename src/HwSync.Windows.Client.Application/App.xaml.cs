using System.IO;
using System.Windows;
using HwSync.Windows.AppServices.Client;
using Microsoft.Extensions.DependencyInjection;

namespace HwSync.Windows.Client.Application
{
    /// <summary>
    /// Запуск WPF-оболочки и управление контейнером зависимостей.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private ServiceProvider? _services;

        /// <summary>
        /// Собирает зависимости и открывает окно с моделью из контейнера.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            IServiceCollection services = new ServiceCollection();
            services.AddWindowsClientApplication(AppContext.BaseDirectory);
            ClientStorageBootstrap.AddClientStorage(services);
            services.AddTransient<MainWindow>();
            _services = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            try
            {
                ClientStorageBootstrap.Initialize(_services);
                MainWindow window = _services.GetRequiredService<MainWindow>();
                MainWindow = window;
                window.Show();
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or ArgumentException)
            {
                MessageBox.Show(exception.Message, "Ошибка запуска клиента", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        /// <summary>
        /// Освобождает службы, отменяя работу модели и закрывая HTTP-клиенты.
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            _services?.Dispose();
            base.OnExit(e);
        }
    }
}
