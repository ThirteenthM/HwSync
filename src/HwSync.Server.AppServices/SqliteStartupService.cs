using HwSync.Abstractions.FileSystem;
using HwSync.Persistence.Sqlite.Migrations;
using Microsoft.Extensions.Hosting;

namespace HwSync.Server.AppServices
{
    /// <summary>
    /// Подготовка базы до запуска обработчика заданий и HTTP-сервера.
    /// </summary>
    public sealed class SqliteStartupService : IHostedService
    {
        private readonly SqliteMigrator _migrator;

        /// <summary>
        /// Принимает миграции и проверяет список разрешённых каталогов до открытия базы.
        /// </summary>
        public SqliteStartupService(SqliteMigrator migrator, IAllowedServerRoots roots)
        {
            ArgumentNullException.ThrowIfNull(roots);
            _migrator = migrator;
        }

        /// <summary>
        /// Применяет схему; ошибка прерывает запуск Host.
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _migrator.Apply();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Завершает службу без постоянно открытого подключения.
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
