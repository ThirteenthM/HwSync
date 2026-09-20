using System.Globalization;
using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using Microsoft.Data.Sqlite;

namespace HwSync.Persistence.Sqlite
{
    /// <summary>
    /// Снимки папок и события удаления в собственной базе участника.
    /// </summary>
    public sealed class SqliteFolderHistory : IFolderHistory
    {
        private readonly SqliteDatabase _database;

        /// <summary>
        /// Принимает базу с уже применёнными миграциями.
        /// </summary>
        public SqliteFolderHistory(SqliteDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// Атомарно сохраняет успешный снимок и отмечает исчезнувшие известные файлы.
        /// </summary>
        public void RecordSnapshot(string rootPath, IReadOnlyCollection<FileSnapshot> snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            _database.EnsureOutside(rootPath);
            Dictionary<string, FileSnapshot> current = snapshot.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
            using SqliteConnection connection = _database.OpenConnection();
            using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            string root = NormalizeRoot(rootPath);
            Run(command, "INSERT INTO folders(folder_id, root_path) VALUES($id, $root) ON CONFLICT(root_path) DO NOTHING",
                ("$id", Guid.NewGuid().ToString("N")), ("$root", root));
            Set(command, "SELECT folder_id FROM folders WHERE root_path=$root", ("$root", root));
            string folderId = (string)command.ExecuteScalar()!;
            Set(command, "SELECT relative_path, size, modified_utc FROM file_snapshots WHERE folder_id=$folder", ("$folder", folderId));
            List<FileSnapshot> previous = [];
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    previous.Add(new(reader.GetString(0), reader.GetInt64(1), ParseUtc(reader.GetString(2))));
                }
            }

            foreach (FileSnapshot file in previous.Where(file => !current.ContainsKey(file.RelativePath)))
            {
                Run(command, """
                    INSERT INTO deletion_events(folder_id, origin_participant_id, relative_path, deleted_utc,
                        previous_size, previous_modified_utc, active)
                    VALUES($folder, (SELECT participant_id FROM participant WHERE singleton=1), $path, $deleted, $size, $modified, 1)
                    """, ("$folder", folderId), ("$path", file.RelativePath), ("$deleted", DateTimeOffset.UtcNow.ToString("O")),
                    ("$size", file.Size), ("$modified", file.LastWriteTimeUtc.ToString("O")));
            }

            IReadOnlyList<DeletedFile> deleted = ReadDeleted(command, folderId);
            foreach (DeletedFile file in deleted.Where(file => file.Deleted && current.ContainsKey(file.RelativePath)))
            {
                Run(command, "UPDATE deletion_events SET active=0 WHERE event_number=$number", ("$number", file.ChangeNumber));
            }

            Run(command, "DELETE FROM file_snapshots WHERE folder_id=$folder", ("$folder", folderId));
            foreach (FileSnapshot file in current.Values)
            {
                Run(command, "INSERT INTO file_snapshots VALUES($folder, $path, $size, $modified)",
                    ("$folder", folderId), ("$path", file.RelativePath), ("$size", file.Size),
                    ("$modified", file.LastWriteTimeUtc.ToString("O")));
            }

            transaction.Commit();
        }

        /// <summary>
        /// Возвращает журнал папки, включая неактивные события после восстановления файла.
        /// </summary>
        public IReadOnlyList<DeletedFile> GetDeletedFiles(string rootPath)
        {
            _database.EnsureOutside(rootPath);
            using SqliteConnection connection = _database.OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            Set(command, "SELECT folder_id FROM folders WHERE root_path=$root", ("$root", NormalizeRoot(rootPath)));
            return command.ExecuteScalar() is string folderId ? ReadDeleted(command, folderId) : [];
        }

        /// <summary>
        /// Читает события в порядке устойчивого номера участника.
        /// </summary>
        private static IReadOnlyList<DeletedFile> ReadDeleted(SqliteCommand command, string folderId)
        {
            Set(command, """
                SELECT relative_path, active, deleted_utc, event_number, previous_size, previous_modified_utc
                FROM deletion_events WHERE folder_id=$folder ORDER BY event_number
                """, ("$folder", folderId));
            List<DeletedFile> files = [];
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                files.Add(new(reader.GetString(0), reader.GetBoolean(1),
                    DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture), reader.GetInt64(3),
                    new(reader.GetString(0), reader.GetInt64(4), ParseUtc(reader.GetString(5)))));
            }

            return files;
        }

        /// <summary>
        /// Приводит путь к ключу, независимому от регистра Windows.
        /// </summary>
        private static string NormalizeRoot(string rootPath) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath)).ToUpperInvariant();

        /// <summary>
        /// Восстанавливает время снимка без смены часового пояса.
        /// </summary>
        private static DateTime ParseUtc(string value) =>
            DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        /// <summary>
        /// Задаёт SQL и параметры без конкатенации пользовательских значений.
        /// </summary>
        private static void Set(SqliteCommand command, string sql, params (string Name, object Value)[] parameters)
        {
            command.CommandText = sql;
            command.Parameters.Clear();
            foreach ((string name, object value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }
        }

        /// <summary>
        /// Выполняет изменение в текущей транзакции.
        /// </summary>
        private static void Run(SqliteCommand command, string sql, params (string Name, object Value)[] parameters)
        {
            Set(command, sql, parameters);
            command.ExecuteNonQuery();
        }
    }
}
