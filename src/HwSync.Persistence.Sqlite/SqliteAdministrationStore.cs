using System.Globalization;
using HwSync.Abstractions.Administration;
using Microsoft.Data.Sqlite;

namespace HwSync.Persistence.Sqlite
{
    /// <summary>
    /// Чтение административных сведений из базы сервера.
    /// </summary>
    public sealed class SqliteAdministrationStore : IAdministrationStore
    {
        private readonly SqliteDatabase _database;

        /// <summary>
        /// Принимает базу с применёнными миграциями.
        /// </summary>
        public SqliteAdministrationStore(SqliteDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// Читает папки и количество файлов и активных отметок удаления.
        /// </summary>
        public IReadOnlyList<RegisteredFolder> GetFolders()
        {
            using SqliteConnection connection = _database.OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT f.folder_id, f.root_path,
                    (SELECT count(*) FROM file_snapshots s WHERE s.folder_id=f.folder_id),
                    (SELECT count(*) FROM deletion_events d WHERE d.folder_id=f.folder_id AND d.active=1)
                FROM folders f ORDER BY f.root_path
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            List<RegisteredFolder> folders = [];
            while (reader.Read())
            {
                folders.Add(new(reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3)));
            }

            return folders;
        }

        /// <summary>
        /// Читает страницу событий по устойчивому номеру.
        /// </summary>
        public IReadOnlyList<DeletionEntry>? GetDeletions(string folderId, long after, int limit)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(after);
            if (limit is < 1 or > 501)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            using SqliteConnection connection = _database.OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM folders WHERE folder_id=$folder";
            command.Parameters.AddWithValue("$folder", folderId);
            if ((long)command.ExecuteScalar()! == 0)
            {
                return null;
            }

            command.CommandText = """
                SELECT event_number, relative_path, origin_participant_id, deleted_utc,
                    previous_size, previous_modified_utc, active
                FROM deletion_events WHERE folder_id=$folder AND event_number>$after
                ORDER BY event_number LIMIT $limit
                """;
            command.Parameters.AddWithValue("$after", after);
            command.Parameters.AddWithValue("$limit", limit);
            using SqliteDataReader reader = command.ExecuteReader();
            List<DeletionEntry> entries = [];
            while (reader.Read())
            {
                entries.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                    DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture), reader.GetInt64(4),
                    DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture), reader.GetBoolean(6)));
            }

            return entries;
        }
    }
}
