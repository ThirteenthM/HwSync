using HwSync.Persistence.Sqlite.Entities;
using Microsoft.EntityFrameworkCore;

namespace HwSync.Persistence.Sqlite
{
    /// <summary>
    /// Модель собственной базы участника синхронизации.
    /// </summary>
    public sealed class SyncDbContext : DbContext
    {
        public DbSet<ParticipantEntity> Participants => Set<ParticipantEntity>();
        public DbSet<FolderEntity> Folders => Set<FolderEntity>();
        public DbSet<FileSnapshotEntity> FileSnapshots => Set<FileSnapshotEntity>();
        public DbSet<DeletionEventEntity> DeletionEvents => Set<DeletionEventEntity>();
        public DbSet<AcknowledgedStateEntity> AcknowledgedStates => Set<AcknowledgedStateEntity>();

        /// <summary>
        /// Принимает настройки подключения и сборки миграций.
        /// </summary>
        public SyncDbContext(DbContextOptions<SyncDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Явно задаёт ключи, ограничения и отображение времени.
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ParticipantEntity>(entity =>
            {
                entity.ToTable("participant", table => table.HasCheckConstraint("ck_participant_singleton", "Id = 1"));
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedNever();
                entity.HasIndex(item => item.ParticipantId).IsUnique();
            });
            modelBuilder.Entity<FolderEntity>(entity =>
            {
                entity.ToTable("folders");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.RootPath).UseCollation("NOCASE");
                entity.HasIndex(item => item.RootPath).IsUnique();
            });
            modelBuilder.Entity<FileSnapshotEntity>(entity =>
            {
                entity.ToTable("file_snapshots", table => table.HasCheckConstraint("ck_snapshot_size", "Size >= 0"));
                entity.HasKey(item => new { item.FolderId, item.RelativePath });
                entity.Property(item => item.RelativePath).UseCollation("NOCASE");
                entity.Property(item => item.ModifiedUtc).HasConversion(
                    value => value.Ticks, value => new DateTime(value, DateTimeKind.Utc));
                entity.HasOne<FolderEntity>().WithMany().HasForeignKey(item => item.FolderId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<DeletionEventEntity>(entity =>
            {
                entity.ToTable("deletion_events", table => table.HasCheckConstraint("ck_deletion_size", "PreviousSize >= 0"));
                entity.HasKey(item => item.Number);
                entity.Property(item => item.Number).ValueGeneratedOnAdd();
                entity.Property(item => item.RelativePath).UseCollation("NOCASE");
                entity.Property(item => item.DeletedUtc).HasConversion(
                    value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero));
                entity.Property(item => item.PreviousModifiedUtc).HasConversion(
                    value => value.Ticks, value => new DateTime(value, DateTimeKind.Utc));
                entity.HasIndex(item => new { item.FolderId, item.Number });
                entity.HasOne<FolderEntity>().WithMany().HasForeignKey(item => item.FolderId).OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<AcknowledgedStateEntity>(entity =>
            {
                entity.ToTable("acknowledged_states");
                entity.HasKey(item => new { item.ClientId, item.FolderId });
                entity.HasMany(item => item.Files).WithOne()
                    .HasForeignKey(item => new { item.ClientId, item.FolderId }).OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<AcknowledgedFileEntity>(entity =>
            {
                entity.ToTable("acknowledged_files");
                entity.HasKey(item => new { item.ClientId, item.FolderId, item.RelativePath });
            });
        }
    }
}