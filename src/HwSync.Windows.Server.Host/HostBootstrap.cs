using HwSync.Server.AppServices;

namespace HwSync.Windows.Server.Host
{
    /// <summary>
    /// Настройка окружения Windows и подключение общих серверных служб.
    /// </summary>
    public static class HostBootstrap
    {
        /// <summary>
        /// Создаёт Host для консольного запуска или службы Windows.
        /// </summary>
        public static WebApplicationBuilder CreateBuilder(bool consoleMode, string[] configurationArgs)
        {
            WebApplicationOptions settings = new()
            {
                Args = configurationArgs,
                ContentRootPath = AppContext.BaseDirectory
            };
            WebApplicationBuilder builder = WebApplication.CreateBuilder(settings);
            if (!consoleMode)
            {
                builder.Services.AddWindowsService(options => options.ServiceName = "HwSync");
            }

            string defaultDatabasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HwSync", "Server", "state.db");
            builder.Services.AddServerAppServices(builder.Configuration, defaultDatabasePath);
            return builder;
        }
    }
}
