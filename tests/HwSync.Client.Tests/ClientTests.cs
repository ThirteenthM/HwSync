using System.Net;
using System.Net.Http;
using System.Text;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Client.Windows.ViewModels;

namespace HwSync.Client.Tests
{
    public class ClientTests
    {
        [TestCase(400)]
        [TestCase(403)]
        [TestCase(404)]
        [TestCase(409)]
        [TestCase(500)]
        public void HttpFailure_PreservesStatusAndProblemDetails(int status)
        {
            using HttpClient http = new(new ResponseHandler(request => new((HttpStatusCode)status)
            {
                Content = new StringContent("{\"detail\":\"server detail\"}", Encoding.UTF8, "application/problem+json")
            }));
            IHwSyncApiClient client = new HwSyncApiClient(http, new("http://localhost:5080"));
            HwSyncApiException? error = Assert.ThrowsAsync<HwSyncApiException>(() => client.GetHealthAsync());
            Assert.That(error!.StatusCode, Is.EqualTo((HttpStatusCode)status));
            Assert.That(error.Message, Does.Contain("server detail"));
        }

        [Test]
        public void InvalidJson_IsReportedAsContractError()
        {
            using HttpClient http = new(new ResponseHandler(request => new(HttpStatusCode.OK) { Content = new StringContent("not json") }));
            IHwSyncApiClient client = new HwSyncApiClient(http, new("http://localhost:5080"));
            Assert.ThrowsAsync<System.IO.InvalidDataException>(() => client.GetHealthAsync());
        }

        [Test]
        public async Task Cancel_UsesPostAndKeepsBasePath()
        {
            Guid id = Guid.NewGuid();
            using HttpClient http = new(new ResponseHandler(request =>
            {
                Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(request.RequestUri!.AbsolutePath, Is.EqualTo($"/prefix/api/v1/scan-jobs/{id}/cancel"));
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"id\":\"{id}\",\"status\":\"Cancelled\",\"createdAt\":\"2026-09-15T00:00:00Z\"}}", Encoding.UTF8, "application/json")
                };
            }));
            IHwSyncApiClient client = new HwSyncApiClient(http, new("http://localhost:5080/prefix"));
            ScanJobResponse job = await client.CancelScanAsync(id);
            Assert.That(job.Status, Is.EqualTo(ScanJobState.Cancelled));
        }

        [Test]
        public async Task PollFailure_CanResumeWithoutStartingDuplicateJob()
        {
            StubClient client = new();
            using MainViewModel model = new(address => client, new StubSnapshotProvider()) { ClientRootPath = @"D:\client-folder", RootPath = @"D:\server-folder" };
            await model.StartCommand.ExecuteAsync(null);
            Assert.That(model.HasActiveJob, Is.True);
            Assert.That(model.ResumeCommand.CanExecute(null), Is.True);
            Assert.That(model.StartCommand.CanExecute(null), Is.False);
            client.FailPoll = false;
            await model.ResumeCommand.ExecuteAsync(null);
            Assert.That(model.HasActiveJob, Is.False);
            Assert.That(model.Changes, Has.Count.EqualTo(1));
            Assert.That(client.Starts, Is.EqualTo(1));
        }

        [Test]
        public async Task Cancel_SendsRequestToServer()
        {
            StubClient client = new();
            using MainViewModel model = new(address => client, new StubSnapshotProvider()) { ClientRootPath = @"D:\client-folder", RootPath = @"D:\server-folder" };
            await model.StartCommand.ExecuteAsync(null);
            await model.CancelCommand.ExecuteAsync(null);
            Assert.That(client.Cancels, Is.EqualTo(1));
            Assert.That(model.HasActiveJob, Is.False);
            Assert.That(model.Status, Is.EqualTo("Задание отменено"));
        }

        [Test]
        public async Task MissingLocalFolder_DoesNotSendServerRequest()
        {
            StubClient client = new();
            using MainViewModel model = new(address => client, new HwSync.Infrastructure.FileSystem.DirectorySnapshotProvider())
            {
                ClientRootPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString()),
                RootPath = @"D:\server-folder"
            };
            await model.StartCommand.ExecuteAsync(null);
            Assert.That(client.Starts, Is.Zero);
            Assert.That(model.Error, Is.Not.Empty);
            Assert.That(model.HasActiveJob, Is.False);
        }
        private sealed class ResponseHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
            public ResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) { _respond = respond; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(_respond(request));
            }
        }

        private sealed class StubClient : IHwSyncApiClient
        {
            private readonly Guid _id = Guid.NewGuid();
            public bool FailPoll { get; set; } = true;
            public int Starts { get; private set; }
            public int Cancels { get; private set; }
            public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(new HealthResponse("ok"));
            public Task<ScanJobResponse> StartComparisonAsync(CompareFoldersRequest request, CancellationToken cancellationToken = default)
            {
                Starts++;
                Assert.That(request.ClientSnapshot.Single().RelativePath, Is.EqualTo("client.txt"));
                return Task.FromResult(Job(ScanJobState.Queued));
            }
            public Task<ScanJobResponse> GetScanAsync(Guid id, CancellationToken cancellationToken = default)
            {
                if (FailPoll) { throw new HttpRequestException("offline"); }
                return Task.FromResult(Job(ScanJobState.Completed) with
                {
                    Changes = [new(FileChangeKind.Created, null, new("sample.txt", 7, DateTime.UtcNow))]
                });
            }
            public Task<ScanJobResponse> CancelScanAsync(Guid id, CancellationToken cancellationToken = default)
            {
                Cancels++;
                return Task.FromResult(Job(ScanJobState.Cancelled));
            }
            private ScanJobResponse Job(ScanJobState state) => new(_id, state, DateTimeOffset.UtcNow, null, null, null);
        }
    }
}
