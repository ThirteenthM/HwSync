using HwSync.Api;
using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Services;
using HwSync.Core.Services;
using HwSync.Infrastructure.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HwSync.Host
{
    public static class HostBootstrap
    {
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

            builder.Services.AddSingleton<IFolderHistory>(provider => new JsonFolderHistory(
                builder.Configuration["History:Directory"] ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HwSync", "ServerHistory")));
            builder.Services.AddTransient<ISourceFileReader, SourceFileReader>();
            builder.Services.AddTransient<IComparedFileOperations, ComparedFileOperations>();
            builder.Services.AddTransient<IFileSnapshotProvider, DirectorySnapshotProvider>();
            builder.Services.AddTransient<IChangeComparer, ChangeComparer>();
            builder.Services.AddTransient<IChangeScanner, DirectoryChangeScanner>();
            builder.Services.AddSingleton<ScanJobService>();
            builder.Services.AddSingleton<IScanJobService>(provider => provider.GetRequiredService<ScanJobService>());
            builder.Services.AddHwSyncApi();
            builder.Services.AddHostedService<SyncWorker>();

            return builder;
        }
    }
}
