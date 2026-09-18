using HwSync.Api;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HwSync.Api.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace HwSync.Host.Tests
{
    /// <summary>
    /// Проверки жизненного цикла заданий через API.
    /// </summary>
    public class ScanJobApiTests
    {
        /// <summary>
        /// Проверяет завершение задания и отклонение неверных запросов.
        /// </summary>
        [Test]
        public async Task Api_ScanCompletes_AndInvalidRequestsAreRejected()
        {
            string directory = Path.Combine(Path.GetTempPath(), "HwSync-Api-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "sample.txt"), "content");
            WebApplicationBuilder builder = HostBootstrap.CreateBuilder(true, ["Urls=http://127.0.0.1:0"]);
            builder.Configuration["History:Directory"] = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "test-history", Guid.NewGuid().ToString("N"));
            builder.Logging.ClearProviders();
            await using WebApplication app = builder.Build();
            app.MapHwSyncApi();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
            JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
            json.Converters.Add(new JsonStringEnumConverter());
            try
            {
                await app.StartAsync(timeout.Token);
                using HttpClient client = new()
                {
                    BaseAddress = new Uri(app.Urls.Single())
                };
                using HttpResponseMessage health = await client.GetAsync("/health", timeout.Token);
                Assert.That(health.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/scan-jobs",
                    new CompareFoldersRequest(directory, []), timeout.Token);
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
                ScanJobResponse accepted = (await response.Content.ReadFromJsonAsync<ScanJobResponse>(json, timeout.Token))!;
                Assert.That(response.Headers.Location?.ToString(), Is.EqualTo($"/api/v1/scan-jobs/{accepted.Id}"));
                ScanJobResponse job;
                do
                {
                    await Task.Delay(20, timeout.Token);
                    job = (await client.GetFromJsonAsync<ScanJobResponse>($"/api/v1/scan-jobs/{accepted.Id}", json, timeout.Token))!;
                } while (job.FinishedAt is null);
                Assert.Multiple(() =>
                {
                    Assert.That(job.Status, Is.EqualTo(ScanJobState.Completed));
                    Assert.That(job.Changes, Has.Count.EqualTo(1));
                    Assert.That(job.Changes!.Single().Current!.RelativePath, Is.EqualTo("sample.txt"));
                });
                using HttpResponseMessage cancel = await client.PostAsync($"/api/v1/scan-jobs/{accepted.Id}/cancel", null, timeout.Token);
                ScanJobResponse terminal = (await cancel.Content.ReadFromJsonAsync<ScanJobResponse>(json, timeout.Token))!;
                Assert.That(terminal.Status, Is.EqualTo(ScanJobState.Completed));
                using HttpResponseMessage invalid = await client.PostAsJsonAsync("/api/v1/scan-jobs",
                    new
                    {
                        serverRootPath = "relative",
                        clientSnapshot = Array.Empty<FileSnapshotDto>()
                    }, timeout.Token);
                Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                using HttpResponseMessage missing = await client.GetAsync($"/api/v1/scan-jobs/{Guid.NewGuid()}", timeout.Token);
                Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                // Проверяем клиентскую библиотеку против реального HTTP API.
                HwSync.Api.Client.IHwSyncApiClient api = new HwSync.Api.Client.HwSyncApiClient(client, client.BaseAddress!);
                Assert.That((await api.GetHealthAsync(timeout.Token)).Status, Is.EqualTo("ok"));
                ScanJobResponse clientJob = await api.StartComparisonAsync(new(directory, []), timeout.Token);
                do
                {
                    await Task.Delay(20, timeout.Token);
                    clientJob = await api.GetScanAsync(clientJob.Id, timeout.Token);
                } while (clientJob.FinishedAt is null);
                Assert.That(clientJob.Status, Is.EqualTo(ScanJobState.Completed));
                Assert.That(clientJob.Changes!.Single().Current!.RelativePath, Is.EqualTo("sample.txt"));
                Assert.That((await api.CancelScanAsync(clientJob.Id, timeout.Token)).Status, Is.EqualTo(ScanJobState.Completed));
            }
            finally
            {
                await app.StopAsync(CancellationToken.None);
                File.Delete(Path.Combine(directory, "sample.txt"));
                Directory.Delete(directory);
            }
        }
    }
}
