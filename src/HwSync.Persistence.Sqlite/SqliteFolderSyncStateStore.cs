using System.Text.Json;
using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using Microsoft.Data.Sqlite;

namespace HwSync.Persistence.Sqlite
{
    /// <summary>
    /// Подтверждённые состояния участников и папок в SQLite.
    /// </summary>
    public sealed class SqliteFolderSyncStateStore : IFolderSyncStateStore
    {
        private readonly SqliteDatabase _database;

        /// <summary>
        /// Принимает базу с уже применёнными миграциями.
        /// </summary>
        public SqliteFolderSyncStateStore(SqliteDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// Читает подтверждённое состояние выбранной пары участник-папка.
        /// </summary>
        public FolderSyncState? Load(Guid clientId, string folderId)
        {
            Validate(clientId, folderId);
            using SqliteConnection connection = _database.OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT state_json FROM acknowledged_states WHERE client_id=$client AND folder_id=$folder";
            command.Parameters.AddWithValue("$client", clientId.ToString("N"));
            command.Parameters.AddWithValue("$folder", folderId);
            if (command.ExecuteScalar() is not string json)
            {
                return null;
            }

            FolderSyncState state = JsonSerializer.Deserialize<FolderSyncState>(json)
                ?? throw new InvalidDataException("Подтверждённое состояние повреждено.");
            if (state.ClientId != clientId || state.FolderId != folderId || state.Files is null)
            {
                throw new InvalidDataException("Состояние принадлежит другому участнику или папке.");
            }

            return state;
        }

        /// <summary>
        /// Атомарно сохраняет состояние после подтверждения синхронизации.
        /// </summary>
        public void Save(FolderSyncState acknowledgedState)
        {
            ArgumentNullException.ThrowIfNull(acknowledgedState);
            ArgumentNullException.ThrowIfNull(acknowledgedState.Files);
            Validate(acknowledgedState.ClientId, acknowledgedState.FolderId);
            using SqliteConnection connection = _database.OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO acknowledged_states(client_id, folder_id, state_json) VALUES($client, $folder, $json)
                ON CONFLICT(client_id, folder_id) DO UPDATE SET state_json=excluded.state_json;
                """;
            command.Parameters.AddWithValue("$client", acknowledgedState.ClientId.ToString("N"));
            command.Parameters.AddWithValue("$folder", acknowledgedState.FolderId);
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(acknowledgedState));
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Проверяет ключ подтверждённого состояния.
        /// </summary>
        private static void Validate(Guid clientId, string folderId)
        {
            if (clientId == Guid.Empty || string.IsNullOrWhiteSpace(folderId))
            {
                throw new ArgumentException("Требуются идентификаторы участника и папки.");
            }
        }
    }
}
