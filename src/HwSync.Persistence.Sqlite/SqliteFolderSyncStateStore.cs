using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using HwSync.Persistence.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HwSync.Persistence.Sqlite
{
    /// <summary>
    /// Объектное хранение подтверждённых версий участников.
    /// </summary>
    public sealed class SqliteFolderSyncStateStore : IFolderSyncStateStore
    {
        private readonly SqliteDatabase _database;

        /// <summary>
        /// Принимает фабрику контекстов с применёнными миграциями.
        /// </summary>
        public SqliteFolderSyncStateStore(SqliteDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// Читает состояние вместе с явно загруженными версиями файлов.
        /// </summary>
        public FolderSyncState? Load(Guid clientId, string folderId)
        {
            Validate(clientId, folderId);
            using SyncDbContext context = _database.CreateContext();
            AcknowledgedStateEntity? state = context.AcknowledgedStates.AsNoTracking().Include(item => item.Files)
                .SingleOrDefault(item => item.ClientId == clientId && item.FolderId == folderId);
            if (state is null)
            {
                return null;
            }

            return new(state.ClientId, state.FolderId,
                state.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                    .Select(file => new FileVersion(file.RelativePath, file.Revision, file.ContentHash)).ToArray());
        }

        /// <summary>
        /// Сохраняет изменения версий в одной транзакции.
        /// </summary>
        public void Save(FolderSyncState acknowledgedState)
        {
            ArgumentNullException.ThrowIfNull(acknowledgedState);
            ArgumentNullException.ThrowIfNull(acknowledgedState.Files);
            Validate(acknowledgedState.ClientId, acknowledgedState.FolderId);
            Dictionary<string, FileVersion> current = acknowledgedState.Files.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
            using SyncDbContext context = _database.CreateContext();
            using IDbContextTransaction transaction = context.Database.BeginTransaction();
            AcknowledgedStateEntity? state = context.AcknowledgedStates.Include(item => item.Files)
                .SingleOrDefault(item => item.ClientId == acknowledgedState.ClientId && item.FolderId == acknowledgedState.FolderId);
            if (state is null)
            {
                state = new() { ClientId = acknowledgedState.ClientId, FolderId = acknowledgedState.FolderId };
                context.AcknowledgedStates.Add(state);
            }

            foreach (AcknowledgedFileEntity file in state.Files.ToArray())
            {
                if (current.Remove(file.RelativePath, out FileVersion? version))
                {
                    file.Revision = version.Revision;
                    file.ContentHash = version.ContentHash;
                }
                else
                {
                    context.Remove(file);
                }
            }

            foreach (FileVersion file in current.Values)
            {
                state.Files.Add(new()
                {
                    ClientId = state.ClientId, FolderId = state.FolderId,
                    RelativePath = file.RelativePath, Revision = file.Revision, ContentHash = file.ContentHash
                });
            }

            context.SaveChanges();
            transaction.Commit();
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