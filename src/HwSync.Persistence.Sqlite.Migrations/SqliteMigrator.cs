using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace HwSync.Persistence.Sqlite.Migrations
{
    /// <summary>
    /// Последовательное обновление схемы с проверкой истории и транзакционным откатом.
    /// </summary>
    public sealed class SqliteMigrator
    {
        private readonly SqliteDatabase _database;
        private readonly IReadOnlyList<SqliteMigration> _migrations;

        /// <summary>
        /// Принимает базу и набор миграций приложения.
        /// </summary>
        public SqliteMigrator(SqliteDatabase database, IReadOnlyList<SqliteMigration>? migrations = null)
        {
            _database = database;
            _migrations = [.. (migrations ?? LoadMigrations())];
            if (_migrations.Select(migration => migration.Version).Where((version, index) => version != index + 1).Any()
                || _migrations.Any(migration => string.IsNullOrWhiteSpace(migration.Name) || string.IsNullOrWhiteSpace(migration.Sql)))
            {
                throw new ArgumentException("Миграции должны иметь последовательные версии начиная с 1 и непустое содержимое.");
            }
        }

        /// <summary>
        /// Применяет недостающие миграции атомарно; при ошибке вызывающий Host прекращает запуск.
        /// </summary>
        public void Apply()
        {
            using SqliteConnection connection = _database.OpenConnection();
            using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    version INTEGER PRIMARY KEY,
                    name TEXT NOT NULL,
                    checksum TEXT NOT NULL,
                    applied_utc TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();
            command.CommandText = "SELECT version, name, checksum FROM schema_migrations ORDER BY version";
            List<(int Version, string Name, string Checksum)> applied = new();
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    applied.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
                }
            }

            for (int index = 0; index < applied.Count; index++)
            {
                if (index >= _migrations.Count || applied[index].Version != index + 1
                    || applied[index].Name != _migrations[index].Name || applied[index].Checksum != Checksum(_migrations[index].Sql))
                {
                    throw new InvalidDataException("Схема базы новее приложения или история миграций изменена. Запуск запрещён.");
                }
            }

            foreach (SqliteMigration migration in _migrations.Skip(applied.Count))
            {
                command.CommandText = migration.Sql;
                command.Parameters.Clear();
                command.ExecuteNonQuery();
                command.CommandText = "INSERT INTO schema_migrations VALUES ($version, $name, $checksum, $utc)";
                command.Parameters.AddWithValue("$version", migration.Version);
                command.Parameters.AddWithValue("$name", migration.Name);
                command.Parameters.AddWithValue("$checksum", Checksum(migration.Sql));
                command.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        /// <summary>
        /// Загружает начальную схему из ресурсов проекта миграций.
        /// </summary>
        private static IReadOnlyList<SqliteMigration> LoadMigrations()
        {
            using Stream stream = typeof(SqliteMigrator).Assembly.GetManifestResourceStream(
                "HwSync.Persistence.Sqlite.Migrations.Scripts.0001_initial.sql")
                ?? throw new InvalidOperationException("Ресурс начальной миграции не найден.");

            using StreamReader reader = new(stream);
            return [new(1, "Initial schema", reader.ReadToEnd())];
        }

        /// <summary>
        /// Считает контрольную сумму SQL независимо от переносов строк.
        /// </summary>
        private static string Checksum(string sql) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql.ReplaceLineEndings(((char)10).ToString()))));
    }
}
