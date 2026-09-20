using System.IO;
using System.Text.Json;
using System.Windows;
using HwSync.Windows.AppServices.Administration;
using Microsoft.Extensions.DependencyInjection;

namespace HwSync.Windows.Admin.Host
{
    /// <summary>
    /// Запуск отдельной WPF-утилиты администрирования.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private ServiceProvider? _services;

        /// <summary>
        /// Собирает зависимости и открывает окно без локального хранилища синхронизации.
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                IServiceCollection services = new ServiceCollection();
                services.AddWindowsAdminServices(AppContext.BaseDirectory);
                services.AddTransient<MainWindow>();
                _services = services.BuildServiceProvider(new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true
                });
                MainWindow window = _services.GetRequiredService<MainWindow>();
                MainWindow = window;
                window.Show();
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                MessageBox.Show(exception.Message, "Ошибка запуска утилиты", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        /// <summary>
        /// Отменяет запросы и освобождает службы при выходе.
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            _services?.Dispose();
            base.OnExit(e);
        }
    }
}
