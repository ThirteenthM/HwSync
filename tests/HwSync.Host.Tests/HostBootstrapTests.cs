using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Services;
using HwSync.Core.Services;
using HwSync.Host;
using HwSync.Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HwSync.Host.Tests
{
    public class HostBootstrapTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public async Task Host_OutsideServiceManager_StartsAndStops(bool explicitConsoleMode)
        {
            Microsoft.AspNetCore.Builder.WebApplicationBuilder builder = HostBootstrap.CreateBuilder(explicitConsoleMode, ["Urls=http://127.0.0.1:0"]);
            builder.Configuration["History:Directory"] = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "test-history", Guid.NewGuid().ToString("N"));
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

        [Test]
        public void CreateBuilder_ConsoleFlag_PreservesConfigurationArguments()
        {
            Microsoft.AspNetCore.Builder.WebApplicationBuilder builder = HostBootstrap.CreateBuilder(
                true, ["Logging:LogLevel:Default=Debug"]);
            builder.Configuration["History:Directory"] = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "test-history", Guid.NewGuid().ToString("N"));
            builder.Logging.ClearProviders();
            using IHost host = builder.Build();

            Assert.That(builder.Configuration["Logging:LogLevel:Default"], Is.EqualTo("Debug"));
        }
    }
}
