using System.IO;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Client.Windows.Application;
using HwSync.Client.Windows.Application.ViewModels;
using HwSync.Client.Windows.Contract.Configuration;
using HwSync.Client.Windows.Contract.Services;
using HwSync.Client.Windows.Contract.ViewModels;
using HwSync.Infrastructure.FileSystem;

namespace HwSync.Client.Tests
{
    /// <summary>
    /// Проверки плана и автоматического выполнения файловых операций.
    /// </summary>
    public sealed class AutoSyncTests
    {
        /// <summary>
        /// Проверяет направление и безопасный пропуск при разных правилах.
        /// </summary>
        [TestCase(false, true, SyncRule.Copy, FileSyncDecision.CopyToClient)]
        [TestCase(true, false, SyncRule.Copy, FileSyncDecision.CopyToServer)]
        [TestCase(false, true, SyncRule.Delete, FileSyncDecision.DeleteOnServer)]
        [TestCase(true, false, SyncRule.Delete, FileSyncDecision.DeleteOnClient)]
        [TestCase(true, false, SyncRule.Keep, FileSyncDecision.Skip)]
        [TestCase(false, true, SyncRule.Skip, FileSyncDecision.Skip)]
        [TestCase(true, true, SyncRule.Copy, FileSyncDecision.AskUser)]
        [TestCase(false, false, SyncRule.Copy, FileSyncDecision.AskUser)]
        public void Strategy_ChoosesAction(bool client, bool server, SyncRule rule, FileSyncDecision expected)
        {
            ISyncDecisionService service = new SyncDecisionService();
            Assert.That(service.Decide(client, server, new() { MissingOnClient = rule, ClientOnlyFiles = rule }), Is.EqualTo(expected));
        }

        /// <summary>
        /// Выполняет оба направления, оставляя отличающийся файл без изменений.
        /// </summary>
        [Test]
        public async Task AutoSync_CopiesBothWaysAndSkipsConflict()
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new() { ClientOnlyFiles = SyncRule.Copy });
            await model.StartCommand.ExecuteAsync(null);
            Assert.That(model.Changes.Select(row => row.Action), Is.EqualTo(new[]
            {
                FileSyncDecision.CopyToClient, FileSyncDecision.CopyToServer, FileSyncDecision.AskUser
            }));
            await model.AutoSyncCommand.ExecuteAsync(null);
            Assert.That(File.ReadAllText(Path.Combine(root, "server.txt")), Is.EqualTo("server"));
            Assert.That(api.Uploaded, Is.EqualTo("client"));
            Assert.That(File.ReadAllText(Path.Combine(root, "conflict.txt")), Is.EqualTo("local conflict"));
            Assert.That(api.Downloads, Is.EqualTo(1));
            Assert.That(model.Status, Does.Contain("Выполнено: 2").And.Contain("Требуют решения: 1"));
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.False);
            Assert.That(model.CopyCommand.CanExecute(null), Is.False);
            Assert.That(model.TransferMetrics!.ConfirmedBytes, Is.EqualTo(12));
        }

        /// <summary>
        /// Требует подтверждение и проверяет обе стороны перед удалением.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task AutoSync_ConfirmsDeletions(bool confirmed)
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new()
            {
                MissingOnClient = SyncRule.Delete, ClientOnlyFiles = SyncRule.Delete
            });
            model.ConfirmDeletion = (_, _) => confirmed;
            await model.StartCommand.ExecuteAsync(null);
            await model.AutoSyncCommand.ExecuteAsync(null);
            Assert.That(api.Deletions, Is.EqualTo(confirmed ? 1 : 0));
            Assert.That(api.Verifications, Is.EqualTo(confirmed ? 1 : 0));
            Assert.That(File.Exists(Path.Combine(root, "client.txt")), Is.EqualTo(!confirmed));
            Assert.That(File.Exists(Path.Combine(root, "conflict.txt")), Is.True);
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.EqualTo(!confirmed));
        }

        /// <summary>
        /// Останавливает план при изменившемся после сравнения локальном файле.
        /// </summary>
        [Test]
        public async Task AutoSync_DoesNotDeleteChangedFile()
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new()
            {
                MissingOnClient = SyncRule.Skip, ClientOnlyFiles = SyncRule.Delete
            });
            model.ConfirmDeletion = (_, _) => true;
            await model.StartCommand.ExecuteAsync(null);
            File.WriteAllText(Path.Combine(root, "client.txt"), "changed since comparison");
            await model.AutoSyncCommand.ExecuteAsync(null);
            Assert.That(File.ReadAllText(Path.Combine(root, "client.txt")), Is.EqualTo("changed since comparison"));
            Assert.That(model.Error, Is.Not.Empty);
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.False);
        }

        /// <summary>
        /// Передаёт отмену активной передаче и не запускает следующую операцию.
        /// </summary>
        [Test]
        public async Task AutoSync_CanCancelDownload()
        {
            string root = CreateDirectory();
            TestClient api = new() { WaitForCancellation = true };
            using MainViewModel model = CreateModel(root, api, new() { ClientOnlyFiles = SyncRule.Copy });
            await model.StartCommand.ExecuteAsync(null);
            Task execution = model.AutoSyncCommand.ExecuteAsync(null);
            await api.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await model.CancelCommand.ExecuteAsync(null);
            await execution.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(api.Uploaded, Is.Null);
            Assert.That(File.Exists(Path.Combine(root, "server.txt")), Is.False);
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.False);
            Assert.That(model.CancelCommand.CanExecute(null), Is.False);
        }

        /// <summary>
        /// Сбрасывает план при переключении профиля даже с одинаковыми путями.
        /// </summary>
        [Test]
        public async Task ProfileChange_InvalidatesPlan()
        {
            string root = CreateDirectory();
            using MainViewModel model = CreateModel(root, new(), new());
            await model.StartCommand.ExecuteAsync(null);
            SyncProfile profile = model.SelectedProfile!;
            model.SelectedProfile = new()
            {
                ServerRootPath = profile.ServerRootPath, ClientRootPath = profile.ClientRootPath,
                ServerAddress = profile.ServerAddress, Rules = new() { MissingOnClient = SyncRule.Delete }
            };
            Assert.That(model.Changes, Is.Empty);
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.False);
        }

        /// <summary>
        /// Создаёт независимую папку с локальным файлом и конфликтом.
        /// </summary>
        private static string CreateDirectory()
        {
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "auto-sync", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "client.txt"), "client");
            File.WriteAllText(Path.Combine(root, "conflict.txt"), "local conflict");
            return root;
        }

        /// <summary>
        /// Создаёт модель с настоящими локальными операциями и подставным сервером.
        /// </summary>
        private static MainViewModel CreateModel(string root, TestClient api, ConflictRules rules)
        {
            MainViewModel model = new(_ => api, new DirectorySnapshotProvider(), new ComparedFileOperations(),
                new SourceFileReader(), new MissingFileSynchronizer(), new SyncDecisionService());
            model.LoadProfiles([new()
            {
                ClientRootPath = root, ServerRootPath = Path.Combine(root, "server"), Rules = rules
            }]);
            return model;
        }

        /// <summary>
        /// Сервер сравнения и файловых операций без сетевых запросов.
        /// </summary>
        private sealed class TestClient : IHwSyncApiClient, IFileDownloadClient, IFileMutationClient
        {
            public string? Uploaded { get; private set; }

            public int Downloads { get; private set; }

            public int Deletions { get; private set; }

            public int Verifications { get; private set; }

            public bool WaitForCancellation { get; init; }

            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Возвращает готовность подставного сервера.
            /// </summary>
            public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(new HealthResponse("ok"));

            /// <summary>
            /// Возвращает два односторонних файла и один конфликт.
            /// </summary>
            public Task<ScanJobResponse> StartComparisonAsync(CompareFoldersRequest request, CancellationToken cancellationToken = default)
            {
                FileSnapshotDto local = request.ClientSnapshot.Single(file => file.RelativePath == "client.txt");
                FileSnapshotDto conflict = request.ClientSnapshot.Single(file => file.RelativePath == "conflict.txt");
                return Task.FromResult(new ScanJobResponse(Guid.NewGuid(), ScanJobState.Completed,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    [new(FileChangeKind.Created, null, new("server.txt", 6, DateTime.UtcNow)),
                     new(FileChangeKind.Deleted, local, null),
                     new(FileChangeKind.Modified, conflict, new("conflict.txt", 8, DateTime.UtcNow))], null));
            }

            /// <summary>
            /// Опрос не нужен для завершённого сравнения.
            /// </summary>
            public Task<ScanJobResponse> GetScanAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            /// <summary>
            /// Отмена сравнения не нужна для завершённого задания.
            /// </summary>
            public Task<ScanJobResponse> CancelScanAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            /// <summary>
            /// Передаёт тестовый файл или ожидает отмены.
            /// </summary>
            public async Task DownloadFileAsync(Guid jobId, string relativePath, Stream target, CancellationToken token)
            {
                Downloads++;
                Started.TrySetResult();
                if (WaitForCancellation)
                {
                    await Task.Delay(Timeout.Infinite, token);
                }

                await target.WriteAsync("server"u8.ToArray(), token);
            }

            /// <summary>
            /// Сохраняет переданное клиентом содержимое.
            /// </summary>
            public async Task UploadFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token)
            {
                using StreamReader reader = new(source, leaveOpen: true);
                Uploaded = await reader.ReadToEndAsync(token);
            }

            /// <summary>
            /// Регистрирует удаление на сервере.
            /// </summary>
            public Task DeleteServerFileAsync(Guid jobId, string relativePath, CancellationToken token)
            {
                Deletions++;
                return Task.CompletedTask;
            }

            /// <summary>
            /// Подтверждает отсутствие клиентского файла на сервере.
            /// </summary>
            public Task EnsureServerFileMissingAsync(Guid jobId, string relativePath, CancellationToken token)
            {
                Verifications++;
                return Task.CompletedTask;
            }
        }
    }
}