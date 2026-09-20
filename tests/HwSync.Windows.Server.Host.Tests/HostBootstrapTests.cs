using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Services;
using HwSync.Core.Services;
using HwSync.Windows.Server.Host;
using HwSync.Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HwSync.Windows.Server.Host.Tests
{
    /// <summary>
    /// Проверки запуска и настройки Host.
    /// </summary>
    public class HostBootstrapTests
    {
        /// <summary>
        /// Проверяет запуск и остановку вне диспетчера служб Windows.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task Host_OutsideServiceManager_StartsAndStops(bool explicitConsoleMode)
        {
            Microsoft.AspNetCore.Builder.WebApplicationBuilder builder = HostBootstrap.CreateBuilder(explicitConsoleMode, ["Urls=http://127.0.0.1:0"]);
            builder.Configuration["Storage:DatabasePath"] = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "test-history", Guid.NewGuid().ToString("N"), "state.db");
            builder.Logging.ClearProviders();
            using IHost host = builder.Build();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
            IHostApplicationLifetime lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

            await host.StartAsync(timeout.Token);
            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(lifetime.ApplicationStarted.IsCancellationRequested, Is.True);
                    Assert.That(host.Services.GetRequiredService<IFolderHistory>(), Is.TypeOf<HwSync.Persistence.Sqlite.SqliteFolderHistory>());
                    Assert.That(host.Services.GetRequiredService<IFolderSyncStateStore>(), Is.TypeOf<HwSync.Persistence.Sqlite.SqliteFolderSyncStateStore>());
                    using Microsoft.Data.Sqlite.SqliteConnection connection = host.Services.GetRequiredService<HwSync.Persistence.Sqlite.SqliteDatabase>().OpenConnection();
                    using Microsoft.Data.Sqlite.SqliteCommand command = connection.CreateCommand();
                    command.CommandText = "SELECT count(*) FROM schema_migrations";
                    Assert.That(command.ExecuteScalar(), Is.EqualTo(1));
                    Assert.That(host.Services.GetRequiredService<IFileSnapshotProvider>(),
                        Is.TypeOf<DirectorySnapshotProvider>());
                    Assert.That(host.Services.GetRequiredService<IChangeComparer>(),
                        Is.TypeOf<ChangeComparer>());
                    Assert.That(host.Services.GetRequiredService<IChangeScanner>(),
                        Is.TypeOf<DirectoryChangeScanner>());
                    Assert.That(host.Services.GetRequiredService<IHostEnvironment>().ContentRootPath,
                        Is.EqualTo(AppContext.BaseDirectory));
                });
            }
            finally
            {
                await host.StopAsync(timeout.Token);
            }

            Assert.That(lifetime.ApplicationStopped.IsCancellationRequested, Is.True);
        }

        /// <summary>
        /// Ошибка базы не позволяет объявить сервер готовым.
        /// </summary>
        [Test]
        public async Task Host_InvalidDatabaseStopsStartup()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".db");
            File.WriteAllText(path, "not a sqlite database");
            Microsoft.AspNetCore.Builder.WebApplicationBuilder builder = HostBootstrap.CreateBuilder(true, ["Urls=http://127.0.0.1:0"]);
            builder.Configuration["Storage:DatabasePath"] = path;
            builder.Logging.ClearProviders();
            using IHost host = builder.Build();
            try
            {
                Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => host.StartAsync());
                Assert.That(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested, Is.False);
                Assert.That(File.ReadAllText(path), Is.EqualTo("not a sqlite database"));
            }
            finally
            {
                await host.StopAsync();
                File.Delete(path);
            }
        }
        /// <summary>
        /// Проверяет сохранение настроек при консольном запуске.
        /// </summary>
        [Test]
        public void CreateBuilder_ConsoleFlag_PreservesConfigurationArguments()
        {
            Microsoft.AspNetCore.Builder.WebApplicationBuilder builder = HostBootstrap.CreateBuilder(
                true, ["Logging:LogLevel:Default=Debug"]);
            builder.Configuration["Storage:DatabasePath"] = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "test-history", Guid.NewGuid().ToString("N"), "state.db");
            builder.Logging.ClearProviders();
            using IHost host = builder.Build();

            Assert.That(builder.Configuration["Logging:LogLevel:Default"], Is.EqualTo("Debug"));
        }
    }
}
