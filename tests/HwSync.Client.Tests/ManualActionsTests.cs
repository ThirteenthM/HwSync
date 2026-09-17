using System.IO;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Client.Windows.ViewModels;
using HwSync.Infrastructure.FileSystem;

namespace HwSync.Client.Tests
{
    public class ManualActionsTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task DeleteClient_RequiresConfirmationAndFreshComparison(bool confirmed)
        {
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "manual-client", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "local.txt");
            await File.WriteAllTextAsync(path, "client");
            ManualClient api = new();
            using MainViewModel model = new(_ => api, new DirectorySnapshotProvider())
            { ClientRootPath = root, RootPath = Path.Combine(root, "server"), ConfirmDeletion = (_, _) => confirmed };
            await model.StartCommand.ExecuteAsync(null);
            Assert.That(model.DeleteClientCommand.CanExecute(null), Is.True);
            await model.DeleteClientCommand.ExecuteAsync(null);
            Assert.That(File.Exists(path), Is.EqualTo(!confirmed));
            Assert.That(api.Verifications, Is.EqualTo(confirmed ? 1 : 0));
            Assert.That(model.DeleteClientCommand.CanExecute(null), Is.EqualTo(!confirmed));
            Assert.That(model.UploadCommand.CanExecute(null), Is.EqualTo(!confirmed));
        }

        [Test]
        public async Task Upload_StreamsLocalFileAndInvalidatesComparison()
        {
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "manual-client", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "local.txt"), "client");
            ManualClient api = new();
            using MainViewModel model = new(_ => api, new DirectorySnapshotProvider())
            { ClientRootPath = root, RootPath = Path.Combine(root, "server") };
            await model.StartCommand.ExecuteAsync(null);
            await model.UploadCommand.ExecuteAsync(null);
            Assert.That(api.Uploaded, Is.EqualTo("client"));
            Assert.That(model.UploadCommand.CanExecute(null), Is.False);
        }

        private sealed class ManualClient : IHwSyncApiClient, IFileMutationClient
        {
            public int Verifications { get; private set; }
            public string? Uploaded { get; private set; }
            public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(new HealthResponse("ok"));
            public Task<ScanJobResponse> StartComparisonAsync(CompareFoldersRequest request, CancellationToken cancellationToken = default) =>
                Task.FromResult(new ScanJobResponse(Guid.NewGuid(), ScanJobState.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    request.ClientSnapshot.Select(file => new FileChangeDto(FileChangeKind.Deleted, file, null)).ToArray(), null));
            public Task<ScanJobResponse> GetScanAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<ScanJobResponse> CancelScanAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public async Task UploadFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token)
            {
                using StreamReader reader = new(source, leaveOpen: true);
                Uploaded = await reader.ReadToEndAsync(token);
            }
            public Task DeleteServerFileAsync(Guid jobId, string relativePath, CancellationToken token) => throw new NotSupportedException();
            public Task EnsureServerFileMissingAsync(Guid jobId, string relativePath, CancellationToken token)
            { Verifications++; return Task.CompletedTask; }
        }
    }
}
