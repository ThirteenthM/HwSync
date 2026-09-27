using HwSync.Abstractions.Administration;
using Microsoft.EntityFrameworkCore;

namespace HwSync.Persistence.Sqlite
{
    /// <summary>
    /// Чтение административных сведений через EF Core.
    /// </summary>
    public sealed class SqliteAdministrationStore : IAdministrationStore
    {
        private readonly SqliteDatabase _database;

        /// <summary>
        /// Принимает фабрику контекстов с применёнными миграциями.
        /// </summary>
        public SqliteAdministrationStore(SqliteDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// Читает папки и счётчики файлов и активных удалений.
        /// </summary>
        public IReadOnlyList<RegisteredFolder> GetFolders()
        {
            using SyncDbContext context = _database.CreateContext();
            return context.Folders.AsNoTracking().OrderBy(folder => folder.RootPath)
                .Select(folder => new RegisteredFolder(folder.Id, folder.RootPath,
                    context.FileSnapshots.LongCount(file => file.FolderId == folder.Id),
                    context.DeletionEvents.LongCount(file => file.FolderId == folder.Id && file.Active))).ToArray();
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

            using SyncDbContext context = _database.CreateContext();
            if (!context.Folders.Any(folder => folder.Id == folderId))
            {
                return null;
            }

            return context.DeletionEvents.AsNoTracking().Where(item => item.FolderId == folderId && item.Number > after)
                .OrderBy(item => item.Number).Take(limit).AsEnumerable()
                .Select(item => new DeletionEntry(item.Number, item.RelativePath, item.OriginParticipantId,
                    item.DeletedUtc, item.PreviousSize, new DateTimeOffset(item.PreviousModifiedUtc), item.Active)).ToArray();
        }
    }
}