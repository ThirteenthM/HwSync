using HwSync.Api.Client;
using HwSync.Windows.Contract.Administration;
using HwSync.Windows.Contract.Common.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HwSync.Windows.AppServices.Administration
{
    /// <summary>
    /// Собирает службы административного приложения без регистрации участника синхронизации.
    /// </summary>
    public static class WindowsAdminServices
    {
        /// <summary>
        /// Подключает конфигурацию, авторизованный HTTP-клиент и модель окна.
        /// </summary>
        public static IServiceCollection AddWindowsAdminServices(this IServiceCollection services, string configurationDirectory)
        {
            services.AddSingleton<IConfigurationReader<AdminSettings>, AdminSettingsReader>();
            services.AddSingleton(provider => provider.GetRequiredService<IConfigurationReader<AdminSettings>>()
                .Load(Path.Combine(configurationDirectory, "appsettings.json")));
            services.AddSingleton<Func<Uri, string, IAdministrationApiClient>>(provider =>
            {
                AdminSettings settings = provider.GetRequiredService<AdminSettings>();
                return (address, token) => new AdministrationApiClient(
                    new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                    {
                        Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds)
                    }, address, token);
            });
            services.AddSingleton<IAdminViewModel>(provider => new AdminViewModel(
                provider.GetRequiredService<Func<Uri, string, IAdministrationApiClient>>())
            {
                ServerAddress = provider.GetRequiredService<AdminSettings>().ServerAddress
            });
            return services;
        }
    }
}
