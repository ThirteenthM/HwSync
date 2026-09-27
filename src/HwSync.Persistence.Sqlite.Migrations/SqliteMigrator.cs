using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HwSync.Persistence.Sqlite.Migrations
{
    /// <summary>
    /// Применяет миграции EF Core перед запуском хранилищ.
    /// </summary>
    public sealed class SqliteMigrator
    {
        private readonly SqliteDatabase _database;

        /// <summary>
        /// Принимает настройки собственной базы участника.
        /// </summary>
        public SqliteMigrator(SqliteDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// Обновляет схему; несовместимую базу оставляет без изменений.
        /// </summary>
        public void Apply()
        {
            using SqliteConnection connection = _database.OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_migrations'";
            if (Convert.ToInt64(command.ExecuteScalar()) != 0)
            {
                throw new InvalidDataException("База использует прежние SQL-миграции. Укажите новый файл базы для EF Core; прежняя база не изменена.");
            }

            using SyncDbContext context = _database.CreateContext();
            string[] unknown = context.Database.GetAppliedMigrations().Except(context.Database.GetMigrations()).ToArray();
            if (unknown.Length != 0)
            {
                throw new InvalidDataException("Схема базы новее приложения. Запуск запрещён.");
            }

            context.Database.Migrate();
        }
    }
}