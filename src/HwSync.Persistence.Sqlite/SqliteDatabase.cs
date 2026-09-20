using Microsoft.Data.Sqlite;

namespace HwSync.Persistence.Sqlite
{
    /// <summary>
    /// Подключения к собственной базе участника синхронизации.
    /// </summary>
    public sealed class SqliteDatabase
    {
        public string FilePath { get; }

        /// <summary>
        /// Принимает абсолютный путь к файлу базы вне синхронизируемых папок.
        /// </summary>
        public SqliteDatabase(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !Path.IsPathFullyQualified(filePath))
            {
                throw new ArgumentException("Требуется абсолютный путь к файлу SQLite.", nameof(filePath));
            }

            FilePath = Path.GetFullPath(filePath);
        }

        /// <summary>
        /// Открывает подключение с внешними ключами и ожиданием блокировки.
        /// </summary>
        public SqliteConnection OpenConnection()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            SqliteConnection connection = new(new SqliteConnectionStringBuilder
            {
                DataSource = FilePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                ForeignKeys = true,
                Pooling = false,
                DefaultTimeout = 30
            }.ToString());
            try
            {
                connection.Open();
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Запрещает размещать метаданные внутри обслуживаемой папки.
        /// </summary>
        public void EnsureOutside(string rootPath)
        {
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
            string prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
            if (FilePath.Equals(root, StringComparison.OrdinalIgnoreCase)
                || FilePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("База SQLite должна находиться вне синхронизируемой папки.");
            }
        }
    }
}
