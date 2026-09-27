using HwSync.Abstractions.Models;
using HwSync.Persistence.Sqlite.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
            string identity = (string)Scalar("SELECT ParticipantId FROM participant")!;
            migrations.Apply();
            Assert.That(Scalar("SELECT count(*) FROM __EFMigrationsHistory"), Is.EqualTo(1));
            Assert.That(Scalar("SELECT ParticipantId FROM participant"), Is.EqualTo(identity));
            Assert.That(Guid.ParseExact(identity, "N"), Is.Not.EqualTo(Guid.Empty));
            Assert.That(Scalar("PRAGMA foreign_keys"), Is.EqualTo(1));
        }

        /// <summary>
        /// Прежняя база остаётся нетронутой и требует явного выбора нового файла.
        /// </summary>
        [Test]
        public void Migrations_RejectLegacyDatabase()
        {
            Scalar("CREATE TABLE schema_migrations(version INTEGER); INSERT INTO schema_migrations VALUES(1);");
            Assert.Throws<InvalidDataException>(() => new SqliteMigrator(_database).Apply());
            Assert.That(Scalar("SELECT count(*) FROM schema_migrations"), Is.EqualTo(1));
            Assert.That(Scalar("SELECT count(*) FROM sqlite_master WHERE name='participant'"), Is.EqualTo(0));
        }

        /// <summary>
        /// Не допускает запуск старого приложения с более новой схемой.
        /// </summary>
        [Test]
        public void Migrations_RejectUnknownVersion()
        {
            new SqliteMigrator(_database).Apply();
            Scalar("INSERT INTO __EFMigrationsHistory VALUES ('99999999999999_Future', '10.0.3')");
            Assert.Throws<InvalidDataException>(() => new SqliteMigrator(_database).Apply());
            Assert.That(Scalar("SELECT count(*) FROM __EFMigrationsHistory"), Is.EqualTo(2));
        }

        /// <summary>
        /// Миграция описывает текущую модель полностью.
        /// </summary>
        [Test]
        public void Migrations_MatchModel()
        {
            using SyncDbContext context = _database.CreateContext();
            Assert.That(context.Database.HasPendingModelChanges(), Is.False);
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
            Assert.That(history.GetDeletedFiles(root), Has.Count.EqualTo(2));
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
            Assert.Throws<DbUpdateException>(() => history.RecordSnapshot(root, [new("invalid.txt", -1, DateTime.UnixEpoch)]));
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
            store.Save(new(client, "folder", [new("old.txt", 1, null), new("keep.txt", 2, "hash")]));
            Assert.That(store.Load(client, "folder")!.Files, Has.Count.EqualTo(2));
            store.Save(new(client, "folder", [new("keep.txt", 3, null), new("new.txt", 1, "new-hash")]));
            Assert.That(store.Load(client, "folder")!.Files,
                Is.EquivalentTo(new FileVersion[] { new("keep.txt", 3, null), new("new.txt", 1, "new-hash") }));
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
