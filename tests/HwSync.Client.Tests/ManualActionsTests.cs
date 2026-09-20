using System.IO;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Windows.AppServices.Client.ViewModels;
using HwSync.Windows.Contract.Client.ViewModels;
using HwSync.Infrastructure.FileSystem;

namespace HwSync.Client.Tests
{
    /// <summary>
    /// Проверки ручных команд копирования и удаления.
    /// </summary>
    public class ManualActionsTests
    {
        /// <summary>
        /// Проверяет подтверждение удаления и сброс использованного сравнения.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task DeleteClient_RequiresConfirmationAndFreshComparison(bool confirmed)
        {
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "manual-client", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "local.txt");
            await File.WriteAllTextAsync(path, "client");
            ManualClient api = new();
            using MainViewModel model = new(_ => api, new DirectorySnapshotProvider(), new HwSync.Infrastructure.FileSystem.ComparedFileOperations(), new HwSync.Infrastructure.FileSystem.SourceFileReader(), new HwSync.Infrastructure.FileSystem.MissingFileSynchronizer(), new HwSync.Windows.AppServices.Client.SyncDecisionService(), new HwSync.Infrastructure.FileSystem.ConflictFileOperations(new HwSync.Infrastructure.FileSystem.SourceFileReader()))
            {
                ClientRootPath = root,
                RootPath = Path.Combine(root, "server"),
                ConfirmDeletion = (_, _) => confirmed
            };
            await model.StartCommand.ExecuteAsync(null);
            Assert.That(model.DeleteClientCommand.CanExecute(null), Is.True);
            await model.DeleteClientCommand.ExecuteAsync(null);
            Assert.That(File.Exists(path), Is.EqualTo(!confirmed));
            Assert.That(api.Verifications, Is.EqualTo(confirmed ? 1 : 0));
            Assert.That(model.DeleteClientCommand.CanExecute(null), Is.EqualTo(!confirmed));
            Assert.That(model.UploadCommand.CanExecute(null), Is.EqualTo(!confirmed));
        }

        /// <summary>
        /// Проверяет передачу содержимого и необходимость нового сравнения.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task Upload_StreamsLocalFileAndInvalidatesComparison(bool metricsEnabled)
        {
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "manual-client", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "local.txt"), "client");
            ManualClient api = new();
            using MainViewModel model = new(_ => api, new DirectorySnapshotProvider(), new HwSync.Infrastructure.FileSystem.ComparedFileOperations(), new HwSync.Infrastructure.FileSystem.SourceFileReader(), new HwSync.Infrastructure.FileSystem.MissingFileSynchronizer(), new HwSync.Windows.AppServices.Client.SyncDecisionService(), new HwSync.Infrastructure.FileSystem.ConflictFileOperations(new HwSync.Infrastructure.FileSystem.SourceFileReader()))
            {
                TransferMetricsEnabled = metricsEnabled,
                ClientRootPath = root,
                RootPath = Path.Combine(root, "server")
            };
            await model.StartCommand.ExecuteAsync(null);
            await model.UploadCommand.ExecuteAsync(null);
            Assert.That(api.Uploaded, Is.EqualTo("client"));
            Assert.That(model.UploadCommand.CanExecute(null), Is.False);
            Assert.That(model.HasTransferMetrics, Is.EqualTo(metricsEnabled));
            if (metricsEnabled)
            {
                Assert.That(model.TransferMetrics!.ConfirmedBytes, Is.EqualTo(6));
                Assert.That(model.TransferMetrics.Files.Single().Outcome, Is.EqualTo("Скопирован"));
                Assert.That(model.MetricsSummary, Does.Contain("Клиент → сервер"));
            }
            else
            {
                Assert.That(model.TransferMetrics, Is.Null);
                Assert.That(model.MetricsSummary, Is.Empty);
            }
        }

        /// <summary>
        /// Подставной API-клиент для ручных файловых операций.
        /// </summary>
        private sealed class ManualClient : IHwSyncApiClient, IFileMutationClient
        {
            public int Verifications
            {
                get; private set;
            }

            public string? Uploaded
            {
                get; private set;
            }

            /// <summary>
            /// Запрашивает готовность сервера.
            /// </summary>
            public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(new HealthResponse("ok"));

            /// <summary>
            /// Отправляет снимок клиента и запускает сравнение на сервере.
            /// </summary>
            public Task<ScanJobResponse> StartComparisonAsync(CompareFoldersRequest request, CancellationToken cancellationToken = default) =>
                Task.FromResult(new ScanJobResponse(Guid.NewGuid(), ScanJobState.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    request.ClientSnapshot.Select(file => new FileChangeDto(FileChangeKind.Deleted, file, null)).ToArray(), null));

            /// <summary>
            /// Получает состояние и результат задания.
            /// </summary>
            public Task<ScanJobResponse> GetScanAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            /// <summary>
            /// Запрашивает отмену задания на сервере.
            /// </summary>
            public Task<ScanJobResponse> CancelScanAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            /// <summary>
            /// Передаёт отсутствующий на сервере файл без перезаписи.
            /// </summary>
            public async Task UploadFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token)
            {
                using StreamReader reader = new(source, leaveOpen: true);
                Uploaded = await reader.ReadToEndAsync(token);
            }

            /// <summary>
            /// Удаляет серверный файл из выбранного сравнения.
            /// </summary>
            public Task DeleteServerFileAsync(Guid jobId, string relativePath, CancellationToken token) => throw new NotSupportedException();

            /// <summary>
            /// Проверяет отсутствие файла на сервере перед локальным удалением.
            /// </summary>
            public Task EnsureServerFileMissingAsync(Guid jobId, string relativePath, CancellationToken token)
            {
                Verifications++;
                return Task.CompletedTask;
            }
        }
    }
}
