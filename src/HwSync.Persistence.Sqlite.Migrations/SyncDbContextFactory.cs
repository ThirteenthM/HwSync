using Microsoft.EntityFrameworkCore.Design;

namespace HwSync.Persistence.Sqlite.Migrations
{
    /// <summary>
    /// Создаёт контекст для команд миграций без запуска сервера или клиента.
    /// </summary>
    public sealed class SyncDbContextFactory : IDesignTimeDbContextFactory<SyncDbContext>
    {
        /// <summary>
        /// Использует отдельную служебную базу либо путь из аргумента команды.
        /// </summary>
        public SyncDbContext CreateDbContext(string[] args)
        {
            string path = args.Length == 0 ? Path.GetFullPath("artifacts/ef-design.db") : Path.GetFullPath(args[0]);
            return new SqliteDatabase(path).CreateContext();
        }
    }
}