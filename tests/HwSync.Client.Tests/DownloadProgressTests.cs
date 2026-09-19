using System.IO;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Client.Windows.Application.ViewModels;
using HwSync.Client.Windows.Contract.ViewModels;
using HwSync.Infrastructure.FileSystem;

namespace HwSync.Client.Tests
{
    /// <summary>
    /// Проверки текущего файла в статусе скачивания.
    /// </summary>
    public class DownloadProgressTests
    {
        /// <summary>
        /// Проверяет обновление статуса после скачивания и пропуска существующего файла.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task Copy_ReportsEachFileAndPreservesFinalSummary(bool metricsEnabled)
        {
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "download-progress", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            DownloadClient api = new();
            using MainViewModel model = new(_ => api, new DirectorySnapshotProvider(), new HwSync.Infrastructure.FileSystem.ComparedFileOperations(), new HwSync.Infrastructure.FileSystem.SourceFileReader(), new HwSync.Infrastructure.FileSystem.MissingFileSynchronizer(), new HwSync.Client.Windows.Application.SyncDecisionService())
            {
                TransferMetricsEnabled = metricsEnabled,
                ClientRootPath = root,
                RootPath = Path.Combine(root, "server")
            };
            List<string> downloadStatuses = new();
            List<string> displayedStatuses = new();
            api.OnDownload = () => downloadStatuses.Add(model.Status);
            model.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(model.Status))
                {
                    displayedStatuses.Add(model.Status);
                }
            };
            await model.StartCommand.ExecuteAsync(null);
            await File.WriteAllTextAsync(Path.Combine(root, "second.txt"), "keep");
            await model.CopyCommand.ExecuteAsync(null);
            Assert.That(downloadStatuses, Is.EqualTo(new[]
            {
                "Копирование 1 из 3: first.txt",
                "Копирование 3 из 3: third.txt"
            }));
            Assert.That(displayedStatuses, Does.Contain("Копирование 2 из 3: second.txt"));
            Assert.That(model.Status, Does.Contain("Скопировано: 2. Пропущено или с ошибкой: 1."));
            Assert.That(await File.ReadAllTextAsync(Path.Combine(root, "second.txt")), Is.EqualTo("keep"));
            Assert.That(model.CopyCommand.CanExecute(null), Is.False);
            Assert.That(model.HasTransferMetrics, Is.EqualTo(metricsEnabled));
            if (metricsEnabled)
            {
                Assert.That(model.TransferMetrics!.ConfirmedBytes, Is.EqualTo(2));
                Assert.That(model.TransferMetrics.Files.Count, Is.EqualTo(3));
                Assert.That(model.TransferMetrics.Files[1].ConfirmedBytes, Is.Zero);
                Assert.That(model.MetricsSummary, Does.Contain("Сервер → клиент"));
            }
            else
            {
                Assert.That(model.TransferMetrics, Is.Null);
                Assert.That(model.MetricsSummary, Is.Empty);
            }
        }

        /// <summary>
        /// Проверяет завершение метрик без учёта недокопированного файла при отмене.
        /// </summary>
        [Test]
        public async Task Copy_CancelledFileIsNotCountedAsTransferred()
        {
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "download-progress", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            DownloadClient api = new();
            using MainViewModel model = new(_ => api, new DirectorySnapshotProvider(), new HwSync.Infrastructure.FileSystem.ComparedFileOperations(), new HwSync.Infrastructure.FileSystem.SourceFileReader(), new HwSync.Infrastructure.FileSystem.MissingFileSynchronizer(), new HwSync.Client.Windows.Application.SyncDecisionService())
            {
                ClientRootPath = root,
                RootPath = Path.Combine(root, "server")
            };
            api.OnDownload = () => model.CancelCommand.Execute(null);
            await model.StartCommand.ExecuteAsync(null);
            await model.CopyCommand.ExecuteAsync(null);
            Assert.That(model.TransferMetrics, Is.Not.Null);
            Assert.That(model.TransferMetrics!.ConfirmedBytes, Is.Zero);
            Assert.That(model.TransferMetrics.Files.Single().Outcome, Is.EqualTo("Прерван"));
            Assert.That(model.Status, Does.Contain("отменено"));
            Assert.That(Directory.GetFiles(root), Is.Empty);
        }

        /// <summary>
        /// Подставной сервер с тремя файлами для скачивания.
        /// </summary>
        private sealed class DownloadClient : IHwSyncApiClient, IFileDownloadClient
        {
            public Action? OnDownload
            {
                get; set;
            }

            /// <summary>
            /// Возвращает готовность тестового сервера.
            /// </summary>
            public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(new HealthResponse("ok"));

            /// <summary>
            /// Возвращает завершённое сравнение с тремя серверными файлами.
            /// </summary>
            public Task<ScanJobResponse> StartComparisonAsync(CompareFoldersRequest request, CancellationToken cancellationToken = default) =>
                Task.FromResult(new ScanJobResponse(Guid.NewGuid(), ScanJobState.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    new[] { "first.txt", "second.txt", "third.txt" }.Select(path =>
                        new FileChangeDto(FileChangeKind.Created, null, new(path, 1, DateTime.UnixEpoch))).ToArray(), null));

            /// <summary>
            /// Отклоняет опрос, не требуемый завершённым сравнением.
            /// </summary>
            public Task<ScanJobResponse> GetScanAsync(Guid id, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            /// <summary>
            /// Отклоняет отмену завершённого задания.
            /// </summary>
            public Task<ScanJobResponse> CancelScanAsync(Guid id, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            /// <summary>
            /// Фиксирует статус формы перед асинхронной передачей файла.
            /// </summary>
            public async Task DownloadFileAsync(Guid jobId, string relativePath, Stream destination, CancellationToken cancellationToken = default)
            {
                OnDownload?.Invoke();
                await Task.Yield();
                await destination.WriteAsync(new byte[] { 42 }, cancellationToken);
            }
        }
    }
}
