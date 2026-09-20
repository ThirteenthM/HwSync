using HwSync.Abstractions.Models;
using HwSync.Persistence.Sqlite.Migrations;
using Microsoft.Data.Sqlite;

namespace HwSync.Persistence.Sqlite.Tests
{
    /// <summary>
    /// Проверки миграций и истории на отдельных настоящих базах SQLite.
    /// </summary>
    public sealed class SqliteStorageTests
    {
        private string _directory = "";
        private SqliteDatabase _database = null!;

        /// <summary>
        /// Создаёт независимый путь для каждой проверки.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "sqlite-tests", Guid.NewGuid().ToString("N"));
            _database = new(Path.Combine(_directory, "metadata", "state.db"));
        }

        /// <summary>
        /// Удаляет только созданные тестом файлы.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                foreach (string file in Directory.GetFiles(_directory, "*", SearchOption.AllDirectories))
                {
                    File.Delete(file);
                }

                foreach (string directory in Directory.GetDirectories(_directory, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
                {
                    Directory.Delete(directory);
                }

                Directory.Delete(_directory);
            }
        }

        /// <summary>
        /// Повторный запуск не меняет идентификатор участника и не повторяет миграцию.
        /// </summary>
        [Test]
        public void Migrations_AreRepeatable()
        {
            SqliteMigrator migrations = new(_database);
            migrations.Apply();
            string identity = (string)Scalar("SELECT participant_id FROM participant")!;
            migrations.Apply();
            Assert.That(Scalar("SELECT count(*) FROM schema_migrations"), Is.EqualTo(1));
            Assert.That(Scalar("SELECT participant_id FROM participant"), Is.EqualTo(identity));
            Assert.That(Guid.ParseExact(identity, "N"), Is.Not.EqualTo(Guid.Empty));
            Assert.That(Scalar("PRAGMA foreign_keys"), Is.EqualTo(1));
        }

        /// <summary>
        /// Ошибка следующей миграции откатывает DDL и записи журнала, сохраняя прежние данные.
        /// </summary>
        [Test]
        public void MigrationFailure_RollsBack()
        {
            SqliteMigration first = new(1, "first", "CREATE TABLE example(value TEXT); INSERT INTO example VALUES('saved');");
            new SqliteMigrator(_database, [first]).Apply();
            Assert.Throws<SqliteException>(() => new SqliteMigrator(_database,
                [first, new(2, "broken", "CREATE TABLE partial(value TEXT); SELECT * FROM nonexistent;")]).Apply());
            Assert.That(Scalar("SELECT value FROM example"), Is.EqualTo("saved"));
            Assert.That(Scalar("SELECT count(*) FROM schema_migrations"), Is.EqualTo(1));
            Assert.That(Scalar("SELECT count(*) FROM sqlite_master WHERE name='partial'"), Is.EqualTo(0));
        }

        /// <summary>
        /// Не допускает запуск с изменённой миграцией либо более старым приложением.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void MigrationHistory_RejectsChanges(bool olderApplication)
        {
            SqliteMigration first = new(1, "first", "CREATE TABLE example(value TEXT);");
            new SqliteMigrator(_database, [first]).Apply();
            IReadOnlyList<SqliteMigration> migrations = olderApplication ? [] : [first with { Sql = "SELECT 1;" }];
            Assert.Throws<InvalidDataException>(() => new SqliteMigrator(_database, migrations).Apply());
            Assert.That(Scalar("SELECT count(*) FROM schema_migrations"), Is.EqualTo(1));
        }

        /// <summary>
        /// Новая миграция применяется поверх данных прежней версии.
        /// </summary>
        [Test]
        public void MigrationUpgrade_PreservesData()
        {
            SqliteMigration first = new(1, "first", "CREATE TABLE example(value TEXT); INSERT INTO example VALUES('saved');");
            new SqliteMigrator(_database, [first]).Apply();
            new SqliteMigrator(_database, [first, new(2, "add column", "ALTER TABLE example ADD COLUMN extra INTEGER DEFAULT 7;")]).Apply();
            Assert.That(Scalar("SELECT value FROM example"), Is.EqualTo("saved"));
            Assert.That(Scalar("SELECT extra FROM example"), Is.EqualTo(7));
        }

        /// <summary>
        /// Исчезновение известного файла создаёт одно событие, восстановление снимает его активность.
        /// </summary>
        [Test]
        public void History_TracksDeletionAndRestoration()
        {
            new SqliteMigrator(_database).Apply();
            SqliteFolderHistory history = new(_database);
            string root = Path.Combine(_directory, "files");
            FileSnapshot file = new("данные.txt", 42, DateTime.UnixEpoch);
            history.RecordSnapshot(root, [file]);
            Assert.That(history.GetDeletedFiles(root), Is.Empty);
            history.RecordSnapshot(root, []);
            history.RecordSnapshot(root, []);
            DeletedFile deleted = new SqliteFolderHistory(_database).GetDeletedFiles(root).Single();
            Assert.That(deleted.PreviousFile, Is.EqualTo(file));
            Assert.That(deleted.ChangeNumber, Is.EqualTo(1));
            Assert.That(deleted.Deleted, Is.True);
            history.RecordSnapshot(root, [file]);
            Assert.That(history.GetDeletedFiles(root).Single().Deleted, Is.False);
            history.RecordSnapshot(root, []);
            Assert.That(history.GetDeletedFiles(root).Count, Is.EqualTo(2));
            Assert.That(history.GetDeletedFiles(root).Last().ChangeNumber, Is.EqualTo(2));
        }

        /// <summary>
        /// Некорректный снимок не стирает предыдущее состояние и не оставляет ложные удаления.
        /// </summary>
        [Test]
        public void History_FailedWriteIsAtomic()
        {
            new SqliteMigrator(_database).Apply();
            SqliteFolderHistory history = new(_database);
            string root = Path.Combine(_directory, "files");
            history.RecordSnapshot(root, [new("old.txt", 1, DateTime.UnixEpoch)]);
            Assert.Throws<SqliteException>(() => history.RecordSnapshot(root, [new("invalid.txt", -1, DateTime.UnixEpoch)]));
            Assert.That(history.GetDeletedFiles(root), Is.Empty);
            history.RecordSnapshot(root, []);
            Assert.That(history.GetDeletedFiles(root).Single().RelativePath, Is.EqualTo("old.txt"));
        }

        /// <summary>
        /// Разделяет папки и запрещает базу внутри синхронизируемого корня.
        /// </summary>
        [Test]
        public void History_IsolatesFolders()
        {
            new SqliteMigrator(_database).Apply();
            SqliteFolderHistory history = new(_database);
            string first = Path.Combine(_directory, "first");
            string second = Path.Combine(_directory, "second");
            history.RecordSnapshot(first, [new("file.txt", 1, DateTime.UnixEpoch)]);
            history.RecordSnapshot(second, []);
            history.RecordSnapshot(first, []);
            Assert.That(history.GetDeletedFiles(second), Is.Empty);
            Assert.That(history.GetDeletedFiles(first), Has.Count.EqualTo(1));
            Assert.Throws<IOException>(() => history.RecordSnapshot(_directory, []));
        }

        /// <summary>
        /// Подтверждённые состояния сохраняются независимо для каждого участника.
        /// </summary>
        [Test]
        public void AcknowledgedStates_ArePersistentAndIsolated()
        {
            new SqliteMigrator(_database).Apply();
            Guid client = Guid.NewGuid();
            SqliteFolderSyncStateStore store = new(_database);
            Assert.That(store.Load(client, "folder"), Is.Null);
            store.Save(new(client, "folder", []));
            Assert.That(new SqliteFolderSyncStateStore(_database).Load(client, "folder")!.ClientId, Is.EqualTo(client));
            Assert.That(store.Load(Guid.NewGuid(), "folder"), Is.Null);
            Assert.That(store.Load(client, "other"), Is.Null);
        }

        /// <summary>
        /// Выполняет проверочный запрос к тестовой базе.
        /// </summary>
        private object? Scalar(string sql)
        {
            using SqliteConnection connection = _database.OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteScalar();
        }
    }
}
