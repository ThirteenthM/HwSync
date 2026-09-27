using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using HwSync.Persistence.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HwSync.Persistence.Sqlite
{
    /// <summary>
    /// Снимки папок и события удаления в собственной базе участника.
    /// </summary>
    public sealed class SqliteFolderHistory : IFolderHistory
    {
        private readonly SqliteDatabase _database;

        /// <summary>
        /// Принимает фабрику контекстов с применёнными миграциями.
        /// </summary>
        public SqliteFolderHistory(SqliteDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// Атомарно обновляет снимок и историю исчезновения файлов.
        /// </summary>
        public void RecordSnapshot(string rootPath, IReadOnlyCollection<FileSnapshot> snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            _database.EnsureOutside(rootPath);
            Dictionary<string, FileSnapshot> current = snapshot.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
            using SyncDbContext context = _database.CreateContext();
            using IDbContextTransaction transaction = context.Database.BeginTransaction();
            string root = NormalizeRoot(rootPath);
            FolderEntity? folder = context.Folders.SingleOrDefault(item => item.RootPath == root);
            if (folder is null)
            {
                folder = new() { Id = Guid.NewGuid().ToString("N"), RootPath = root };
                context.Folders.Add(folder);
                context.SaveChanges();
            }

            string folderId = folder.Id;
            string participant = context.Participants.Single().ParticipantId;
            List<FileSnapshotEntity> previous = context.FileSnapshots.Where(item => item.FolderId == folderId).ToList();
            foreach (FileSnapshotEntity file in previous)
            {
                if (current.TryGetValue(file.RelativePath, out FileSnapshot? updated))
                {
                    file.Size = updated.Size;
                    file.ModifiedUtc = updated.LastWriteTimeUtc;
                }
                else
                {
                    context.DeletionEvents.Add(new()
                    {
                        FolderId = folderId,
                        OriginParticipantId = participant,
                        RelativePath = file.RelativePath,
                        DeletedUtc = DateTimeOffset.UtcNow,
                        PreviousSize = file.Size,
                        PreviousModifiedUtc = file.ModifiedUtc,
                        Active = true
                    });
                    context.FileSnapshots.Remove(file);
                }
            }

            HashSet<string> knownPaths = new(previous.Select(file => file.RelativePath), StringComparer.OrdinalIgnoreCase);
            foreach (FileSnapshot file in current.Values.Where(file => !knownPaths.Contains(file.RelativePath)))
            {
                context.FileSnapshots.Add(new()
                {
                    FolderId = folderId, RelativePath = file.RelativePath,
                    Size = file.Size, ModifiedUtc = file.LastWriteTimeUtc
                });
            }

            List<DeletionEventEntity> activeDeletions = context.DeletionEvents
                .Where(item => item.FolderId == folderId && item.Active).ToList();
            foreach (DeletionEventEntity deletion in activeDeletions)
            {
                if (current.ContainsKey(deletion.RelativePath))
                {
                    deletion.Active = false;
                }
            }

            context.SaveChanges();
            transaction.Commit();
        }

        /// <summary>
        /// Читает историю без отслеживания изменений объектов.
        /// </summary>
        public IReadOnlyList<DeletedFile> GetDeletedFiles(string rootPath)
        {
            _database.EnsureOutside(rootPath);
            using SyncDbContext context = _database.CreateContext();
            string root = NormalizeRoot(rootPath);
            string? folderId = context.Folders.Where(folder => folder.RootPath == root).Select(folder => folder.Id).SingleOrDefault();
            if (folderId is null)
            {
                return [];
            }

            return context.DeletionEvents.AsNoTracking().Where(item => item.FolderId == folderId)
                .OrderBy(item => item.Number).AsEnumerable()
                .Select(item => new DeletedFile(item.RelativePath, item.Active, item.DeletedUtc, item.Number,
                    new FileSnapshot(item.RelativePath, item.PreviousSize, item.PreviousModifiedUtc))).ToArray();
        }

        /// <summary>
        /// Приводит путь Windows к устойчивому ключу.
        /// </summary>
        private static string NormalizeRoot(string rootPath) =>
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath)).ToUpperInvariant();
    }
}