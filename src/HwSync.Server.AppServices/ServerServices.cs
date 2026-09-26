using HwSync.Abstractions.Administration;
using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Services;
using HwSync.Api;
using HwSync.Core.Services;
using HwSync.Infrastructure.FileSystem;
using HwSync.Persistence.Sqlite;
using HwSync.Persistence.Sqlite.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HwSync.Server.AppServices
{
    /// <summary>
    /// Общие службы сервера для платформенных точек запуска.
    /// </summary>
    public static class ServerServices
    {
        /// <summary>
        /// Подключает хранилище, API и очередь; путь базы по умолчанию определяет Host.
        /// </summary>
        public static IServiceCollection AddServerAppServices(
            this IServiceCollection services, IConfiguration configuration, string defaultDatabasePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(defaultDatabasePath);
            if (!Path.IsPathFullyQualified(defaultDatabasePath))
            {
                throw new ArgumentException("Путь базы по умолчанию должен быть абсолютным.", nameof(defaultDatabasePath));
            }

            services.AddSingleton(provider =>
            {
                string? path = configuration["Storage:DatabasePath"];
                return new SqliteDatabase(string.IsNullOrEmpty(path) ? defaultDatabasePath : path);
            });
            services.AddSingleton(provider => new SqliteMigrator(provider.GetRequiredService<SqliteDatabase>()));
            services.AddSingleton<IFolderHistory, SqliteFolderHistory>();
            services.AddSingleton<IAdministrationStore, SqliteAdministrationStore>();
            services.AddSingleton(provider => new ServerStorageInfo(provider.GetRequiredService<SqliteDatabase>().FilePath));
            services.AddSingleton<IFolderSyncStateStore, SqliteFolderSyncStateStore>();

            // Миграции должны завершиться до запуска очереди и HTTP-сервера.
            services.AddHostedService<SqliteStartupService>();
            services.AddTransient<ISourceFileReader, SourceFileReader>();
            services.AddTransient<IComparedFileOperations, ComparedFileOperations>();
            services.AddTransient<IConflictFileOperations, ConflictFileOperations>();
            services.AddTransient<IFileSnapshotProvider, DirectorySnapshotProvider>();
            services.AddTransient<IChangeComparer, ChangeComparer>();
            services.AddTransient<IChangeScanner, DirectoryChangeScanner>();
            services.AddSingleton<ScanJobService>();
            services.AddSingleton<IScanJobRunner>(provider => provider.GetRequiredService<ScanJobService>());
            services.AddSingleton<IScanJobService>(provider => provider.GetRequiredService<ScanJobService>());
            services.AddHwSyncApi();
            services.AddHostedService<ScanWorker>();
            return services;
        }
    }
}
