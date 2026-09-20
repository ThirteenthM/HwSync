using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Windows.AppServices.Client;
using HwSync.Windows.AppServices.Client.ViewModels;
using HwSync.Windows.Contract.Client.Configuration;
using HwSync.Windows.Contract.Client.ViewModels;
using HwSync.Infrastructure.FileSystem;

namespace HwSync.Client.Tests
{
    /// <summary>
    /// Проверки приоритета истории удаления над копированием отсутствующих файлов.
    /// </summary>
    public sealed class DeletionSyncTests
    {
        /// <summary>
        /// Совпавшая версия удаляется, изменённая или неизвестная требует решения.
        /// </summary>
        [TestCase(true, false, false, FileSyncDecision.DeleteOnClient)]
        [TestCase(false, false, false, FileSyncDecision.DeleteOnServer)]
        [TestCase(true, true, false, FileSyncDecision.AskUser)]
        [TestCase(false, true, false, FileSyncDecision.AskUser)]
        [TestCase(true, false, true, FileSyncDecision.AskUser)]
        [TestCase(false, false, true, FileSyncDecision.AskUser)]
        public void Strategy_DeletionTakesPriority(bool onServer, bool changed, bool unknown, FileSyncDecision expected)
        {
            FileSnapshot previous = new("file.txt", 6, DateTime.UnixEpoch);
            FileSnapshot remaining = previous with { Size = changed ? 7 : 6 };
            FileDeletionEvidence evidence = new("file.txt", unknown ? null : previous);
            SyncDecisionService decisions = new();
            Assert.That(decisions.Decide(onServer ? remaining : null, onServer ? null : remaining,
                onServer ? null : evidence, onServer ? evidence : null,
                new() { MissingOnClient = SyncRule.Copy, ClientOnlyFiles = SyncRule.Copy }), Is.EqualTo(expected));
        }

        /// <summary>
        /// Без истории отсутствие файла по-прежнему означает обычный односторонний файл.
        /// </summary>
        [TestCase(true, FileSyncDecision.CopyToServer)]
        [TestCase(false, FileSyncDecision.CopyToClient)]
        public void Strategy_NoHistoryDoesNotInventDeletion(bool onClient, FileSyncDecision expected)
        {
            FileSnapshot file = new("file.txt", 6, DateTime.UnixEpoch);
            SyncDecisionService decisions = new();
            Assert.That(decisions.Decide(onClient ? file : null, onClient ? null : file, null, null,
                new() { ClientOnlyFiles = SyncRule.Copy }), Is.EqualTo(expected));
        }

        /// <summary>
        /// Явный режим регистрации истории не восстанавливает и не удаляет оставшуюся копию.
        /// </summary>
        [Test]
        public void Strategy_RecordOnlySkipsUnchangedCopy()
        {
            FileSnapshot file = new("file.txt", 6, DateTime.UnixEpoch);
            Assert.That(new SyncDecisionService().Decide(file, null, null, new("file.txt", file),
                new() { ServerDeletions = SyncRule.RecordOnly, ClientOnlyFiles = SyncRule.Copy }), Is.EqualTo(FileSyncDecision.Skip));
        }

        /// <summary>
        /// Применяет известное удаление в обоих направлениях без восстановления.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task AutoSync_PropagatesDeletion(bool deletedOnServer)
        {
            string root = CreateRoot();
            History history = new();
            HistoryClient api = new() { DeletedOnServer = deletedOnServer };
            if (deletedOnServer)
            {
                WriteFile(root);
            }
            else
            {
                history.Files = [new("file.txt", true, DateTimeOffset.UtcNow, 1, Snapshot)];
            }

            using MainViewModel model = CreateModel(root, api, history);
            await model.StartCommand.ExecuteAsync(null);
            ChangeRow row = model.Changes.Single();
            Assert.That(row.Action, Is.EqualTo(deletedOnServer ? FileSyncDecision.DeleteOnClient : FileSyncDecision.DeleteOnServer));
            Assert.That(row.IsDeletionConflict, Is.True);
            model.ConfirmDeletion = (_, _) => true;

            await model.AutoSyncCommand.ExecuteAsync(null);

            Assert.That(model.Error, Is.Empty);
            Assert.That(File.Exists(Path.Combine(root, "file.txt")), Is.False);
            Assert.That(api.Deleted, Is.EqualTo(!deletedOnServer));
            Assert.That(api.Uploaded, Is.False);
            Assert.That(api.Downloaded, Is.False);
        }

        /// <summary>
        /// Конфликт удаления с изменением можно только явно восстановить или удалить.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task Conflict_RequiresExplicitDecision(bool restore)
        {
            string root = CreateRoot();
            WriteFile(root);
            File.AppendAllText(Path.Combine(root, "file.txt"), " changed");
            HistoryClient api = new();
            using MainViewModel model = CreateModel(root, api, new());
            await model.StartCommand.ExecuteAsync(null);
            ChangeRow row = model.Changes.Single();
            Assert.That(row.Action, Is.EqualTo(FileSyncDecision.AskUser));
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.False);
            model.ChooseConflictResolution = _ => FileSyncDecision.ReplaceOnServer;
            model.ResolveConflictCommand.Execute(row);
            Assert.That(model.Changes.Single().Action, Is.EqualTo(FileSyncDecision.AskUser));

            model.ChooseConflictResolution = _ => restore ? FileSyncDecision.CopyToServer : FileSyncDecision.DeleteOnClient;
            model.ResolveConflictCommand.Execute(row);
            model.ConfirmDeletion = (_, _) => true;
            await model.AutoSyncCommand.ExecuteAsync(null);

            Assert.That(model.Error, Is.Empty);
            Assert.That(api.Uploaded, Is.EqualTo(restore));
            Assert.That(File.Exists(Path.Combine(root, "file.txt")), Is.EqualTo(restore));
        }

        /// <summary>
        /// После локального удаления изменённую серверную версию можно восстановить или удалить явно.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task LocalDeletion_ChangedServerRequiresChoice(bool restore)
        {
            string root = CreateRoot();
            History history = new() { Files = [new("file.txt", true, DateTimeOffset.UtcNow, 1, Snapshot)] };
            HistoryClient api = new() { DeletedOnServer = false, ServerChanged = true };
            using MainViewModel model = CreateModel(root, api, history);
            await model.StartCommand.ExecuteAsync(null);
            ChangeRow row = model.Changes.Single();
            Assert.That(row.Action, Is.EqualTo(FileSyncDecision.AskUser));
            model.ChooseConflictResolution = _ => restore ? FileSyncDecision.CopyToClient : FileSyncDecision.DeleteOnServer;
            model.ResolveConflictCommand.Execute(row);
            model.ConfirmDeletion = (_, _) => true;

            await model.AutoSyncCommand.ExecuteAsync(null);

            Assert.That(model.Error, Is.Empty);
            Assert.That(api.Deleted, Is.EqualTo(!restore));
            Assert.That(api.Downloaded, Is.EqualTo(restore));
            Assert.That(File.Exists(Path.Combine(root, "file.txt")), Is.EqualTo(restore));
        }
        /// <summary>
        /// Ошибка истории блокирует план, а повторный опрос завершает его безопасно.
        /// </summary>
        [Test]
        public async Task HistoryFailure_BlocksUntilResume()
        {
            string root = CreateRoot();
            WriteFile(root);
            HistoryClient api = new() { FailHistory = true };
            using MainViewModel model = CreateModel(root, api, new());
            await model.StartCommand.ExecuteAsync(null);
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.False);
            Assert.That(model.UploadCommand.CanExecute(null), Is.False);
            Assert.That(model.CopyCommand.CanExecute(null), Is.False);
            Assert.That(model.ResumeCommand.CanExecute(null), Is.True);
            api.FailHistory = false;
            await model.ResumeCommand.ExecuteAsync(null);
            Assert.That(model.Changes.Single().Action, Is.EqualTo(FileSyncDecision.DeleteOnClient));
        }

        /// <summary>
        /// Последняя неактивная отметка не блокирует новое появление файла.
        /// </summary>
        [Test]
        public async Task InactiveHistory_UsesOrdinaryRules()
        {
            string root = CreateRoot();
            WriteFile(root);
            HistoryClient api = new() { Inactive = true };
            using MainViewModel model = CreateModel(root, api, new());
            await model.StartCommand.ExecuteAsync(null);
            Assert.That(model.Changes.Single().Action, Is.EqualTo(FileSyncDecision.CopyToServer));
        }

        private static FileSnapshot Snapshot => new("file.txt", 6, DateTime.UnixEpoch);

        /// <summary>
        /// Создаёт изолированную папку клиента.
        /// </summary>
        private static string CreateRoot()
        {
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "deletion-sync", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        /// <summary>
        /// Создаёт копию с атрибутами удалённой версии.
        /// </summary>
        private static void WriteFile(string root)
        {
            File.WriteAllText(Path.Combine(root, "file.txt"), "client");
            File.SetLastWriteTimeUtc(Path.Combine(root, "file.txt"), DateTime.UnixEpoch);
        }

        /// <summary>
        /// Подключает историю и реальные локальные файловые операции.
        /// </summary>
        private static MainViewModel CreateModel(string root, HistoryClient api, History history)
        {
            MainViewModel model = new(_ => api, new DirectorySnapshotProvider(), new ComparedFileOperations(),
                new SourceFileReader(), new MissingFileSynchronizer(), new SyncDecisionService(),
                new ConflictFileOperations(new SourceFileReader()), history);
            model.LoadProfiles([new()
            {
                ClientRootPath = root, ServerRootPath = Path.Combine(root, "server"),
                Rules = new() { MissingOnClient = SyncRule.Copy, ClientOnlyFiles = SyncRule.Copy }
            }]);
            return model;
        }

        /// <summary>
        /// Подставная локальная история для проверки построения плана.
        /// </summary>
        private sealed class History : IFolderHistory
        {
            public IReadOnlyList<DeletedFile> Files { get; set; } = [];

            /// <summary>
            /// Возвращает заданные события.
            /// </summary>
            public IReadOnlyList<DeletedFile> GetDeletedFiles(string rootPath) => Files;

            /// <summary>
            /// Снимки для этой проверки задаются непосредственно тестом.
            /// </summary>
            public void RecordSnapshot(string rootPath, IReadOnlyCollection<FileSnapshot> snapshot)
            {
            }
        }

        /// <summary>
        /// Подставной сервер для проверки истории и доступности операций.
        /// </summary>
        private sealed class HistoryClient : IHwSyncApiClient, IDeletionHistoryClient, IFileMutationClient, IFileDownloadClient
        {
            private ScanJobResponse? _job;

            public bool DeletedOnServer { get; init; } = true;

            public bool FailHistory { get; set; }

            public bool ServerChanged { get; init; }

            public bool Inactive { get; init; }

            public bool Uploaded { get; private set; }

            public bool Downloaded { get; private set; }

            public bool Deleted { get; private set; }

            /// <summary>
            /// Сообщает о готовности сервера.
            /// </summary>
            public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(new HealthResponse("ok"));

            /// <summary>
            /// Формирует одностороннее различие.
            /// </summary>
            public Task<ScanJobResponse> StartComparisonAsync(CompareFoldersRequest request, CancellationToken cancellationToken = default)
            {
                FileChangeDto change = DeletedOnServer
                    ? new(FileChangeKind.Deleted, request.ClientSnapshot.Single(), null)
                    : new(FileChangeKind.Created, null, new("file.txt", ServerChanged ? 7 : 6, DateTime.UnixEpoch));
                _job = new(Guid.NewGuid(), ScanJobState.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [change], null);
                return Task.FromResult(_job);
            }

            /// <summary>
            /// Возвращает сохранённое сравнение для повторного чтения истории.
            /// </summary>
            public Task<ScanJobResponse> GetScanAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_job!);

            /// <summary>
            /// Завершённое задание не требует отмены.
            /// </summary>
            public Task<ScanJobResponse> CancelScanAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_job!);

            /// <summary>
            /// Возвращает удалённую версию либо имитирует недоступность истории.
            /// </summary>
            public Task<IReadOnlyList<DeletedFileDto>> GetDeletedFilesAsync(Guid jobId, CancellationToken token)
            {
                if (FailHistory)
                {
                    throw new IOException("История недоступна");
                }

                return Task.FromResult<IReadOnlyList<DeletedFileDto>>(DeletedOnServer
                    ? [new("file.txt", true, DateTimeOffset.UtcNow, 1, new("file.txt", 6, DateTime.UnixEpoch)),
                       new("file.txt", !Inactive, DateTimeOffset.UtcNow, 2, new("file.txt", 6, DateTime.UnixEpoch))]
                    : []);
            }

            /// <summary>
            /// Регистрирует явное восстановление серверной копии.
            /// </summary>
            public Task UploadFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token)
            {
                Uploaded = true;
                return Task.CompletedTask;
            }

            /// <summary>
            /// Регистрирует распространение удаления на сервер.
            /// </summary>
            public Task DeleteServerFileAsync(Guid jobId, string relativePath, CancellationToken token)
            {
                Deleted = true;
                return Task.CompletedTask;
            }

            /// <summary>
            /// Подтверждает отсутствие удалённой серверной копии.
            /// </summary>
            public Task EnsureServerFileMissingAsync(Guid jobId, string relativePath, CancellationToken token) => Task.CompletedTask;

            /// <summary>
            /// Регистрирует восстановление клиентской копии.
            /// </summary>
            public async Task DownloadFileAsync(Guid jobId, string relativePath, Stream destination, CancellationToken token)
            {
                Downloaded = true;
                await destination.WriteAsync(ServerChanged ? "changed"u8.ToArray() : "client"u8.ToArray(), token);
            }
        }
    }
}
