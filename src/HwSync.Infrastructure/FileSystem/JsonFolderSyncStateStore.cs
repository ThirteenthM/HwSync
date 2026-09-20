using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Infrastructure.FileSystem
{
    /// <summary>
    /// Хранит подтверждённые состояния вне синхронизируемых папок; один процесс записи.
    /// </summary>
    public sealed class JsonFolderSyncStateStore : IFolderSyncStateStore
    {
        private readonly string _directory;
        private readonly System.Threading.Lock _gate = new();

        /// <summary>
        /// Задаёт каталог подтверждённых состояний синхронизации.
        /// </summary>
        public JsonFolderSyncStateStore(string directory)
        {
            _directory = Path.GetFullPath(directory);
        }

        /// <summary>
        /// Читает состояние и проверяет принадлежность клиенту и папке.
        /// </summary>
        public FolderSyncState? Load(Guid clientId, string folderId)
        {
            lock (_gate)
            {
                string path = GetPath(clientId, folderId);
                if (!File.Exists(path))
                {
                    return null;
                }
                FolderSyncState state = JsonSerializer.Deserialize<FolderSyncState>(File.ReadAllText(path))
                    ?? throw new InvalidDataException("Состояние синхронизации повреждено.");
                if (state.ClientId != clientId || state.FolderId != folderId || state.Files is null)
                {
                    throw new InvalidDataException("Состояние принадлежит другой папке или клиенту.");
                }
                return state;
            }
        }

        /// <summary>
        /// Сохраняет подтверждённые версии файлов атомарной заменой состояния.
        /// </summary>
        public void Save(FolderSyncState acknowledgedState)
        {
            ArgumentNullException.ThrowIfNull(acknowledgedState);
            ArgumentNullException.ThrowIfNull(acknowledgedState.Files);
            lock (_gate)
            {
                string path = GetPath(acknowledgedState.ClientId, acknowledgedState.FolderId);
                Directory.CreateDirectory(_directory);
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, JsonSerializer.Serialize(acknowledgedState));
                    File.Move(temporary, path, true);
                }
                finally
                {
                    if (File.Exists(temporary))
                    {
                        File.Delete(temporary);
                    }
                }
            }
        }

        /// <summary>
        /// Получает путь состояния по идентификаторам клиента и папки.
        /// </summary>
        private string GetPath(Guid clientId, string folderId)
        {
            if (clientId == Guid.Empty || string.IsNullOrWhiteSpace(folderId))
            {
                throw new ArgumentException("Требуются идентификаторы клиента и папки.");
            }
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folderId)));
            return Path.Combine(_directory, clientId.ToString("N") + "-" + key + ".json");
        }
    }
}
