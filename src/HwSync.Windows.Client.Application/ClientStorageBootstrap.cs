using HwSync.Abstractions.FileSystem;
using HwSync.Windows.Contract.Client.Configuration;
using HwSync.Infrastructure.FileSystem;
using HwSync.Persistence.Sqlite;
using HwSync.Persistence.Sqlite.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace HwSync.Windows.Client.Application
{
    /// <summary>
    /// Подключение собственной SQLite-базы Windows-клиента.
    /// </summary>
    public static class ClientStorageBootstrap
    {
        /// <summary>
        /// Регистрирует хранение после служб Application.
        /// </summary>
        public static void AddClientStorage(IServiceCollection services)
        {
            services.AddSingleton(provider =>
            {
                ClientSettings settings = provider.GetRequiredService<ClientSettings>();
                return new SqliteDatabase(string.IsNullOrEmpty(settings.DatabasePath)
                    ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HwSync", "Client", "state.db")
                    : settings.DatabasePath);
            });
            services.AddSingleton(provider => new SqliteMigrator(provider.GetRequiredService<SqliteDatabase>()));
            services.AddSingleton<IFolderHistory, SqliteFolderHistory>();
            services.AddSingleton<IFolderSyncStateStore, SqliteFolderSyncStateStore>();
            services.AddSingleton<IFileSnapshotProvider>(provider => new HistorySnapshotProvider(
                new DirectorySnapshotProvider(), provider.GetRequiredService<IFolderHistory>()));
        }

        /// <summary>
        /// Проверяет расположение базы и применяет миграции до открытия окна.
        /// </summary>
        public static void Initialize(IServiceProvider provider)
        {
            SqliteDatabase database = provider.GetRequiredService<SqliteDatabase>();
            foreach (SyncProfile profile in provider.GetRequiredService<List<SyncProfile>>())
            {
                if (!string.IsNullOrWhiteSpace(profile.ClientRootPath))
                {
                    database.EnsureOutside(profile.ClientRootPath);
                }
            }

            provider.GetRequiredService<SqliteMigrator>().Apply();
        }
    }
}
