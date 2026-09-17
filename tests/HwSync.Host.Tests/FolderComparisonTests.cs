using System.Net.Http;
using HwSync.Api;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Infrastructure.FileSystem;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace HwSync.Host.Tests
{
    /// <summary>Проверки сравнения и файловых операций через настоящий HTTP.</summary>
    public class FolderComparisonTests
    {
        /// <summary>Проверяет различия папок и ручную передачу файлов через API.</summary>
        [Test]
        public async Task TwoFolders_ReportDirectionalDifferences_WithoutChangingFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "HwSync-Compare-" + Guid.NewGuid());
            string local = Path.Combine(root, "client");
            string server = Path.Combine(root, "server");
            Directory.CreateDirectory(local);
            Directory.CreateDirectory(server);
            DateTime timestamp = new(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc);
            foreach (string directory in new[]
{
 local, server
})
            {
                await File.WriteAllTextAsync(Path.Combine(directory, "same.txt"), "same");
                File.SetLastWriteTimeUtc(Path.Combine(directory, "same.txt"), timestamp);
                await File.WriteAllTextAsync(Path.Combine(directory, "different.txt"), directory == local ? "x" : "longer");
                File.SetLastWriteTimeUtc(Path.Combine(directory, "different.txt"), timestamp);
            }
            await File.WriteAllTextAsync(Path.Combine(local, "client-only.txt"), "client");
            await File.WriteAllTextAsync(Path.Combine(server, "server-only.txt"), "server");
            WebApplicationBuilder builder = HostBootstrap.CreateBuilder(true, ["Urls=http://127.0.0.1:0"]);
            builder.Configuration["History:Directory"] = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "test-history", Guid.NewGuid().ToString("N"));
            builder.Logging.ClearProviders();
            await using WebApplication app = builder.Build();
            app.MapHwSyncApi();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
            try
            {
                await app.StartAsync(timeout.Token);
                using HttpClient http = new();
                IHwSyncApiClient api = new HwSyncApiClient(http, new(app.Urls.Single()));
                DirectorySnapshotProvider provider = new();
                FileSnapshotDto[] snapshot = provider.GetSnapshot(local).Select(file =>
                    new FileSnapshotDto(file.RelativePath, file.Size, file.LastWriteTimeUtc)).ToArray();
                ScanJobResponse job = await api.StartComparisonAsync(new(server, snapshot), timeout.Token);
                while (job.FinishedAt is null)
                {
                    await Task.Delay(20, timeout.Token);
                    job = await api.GetScanAsync(job.Id, timeout.Token);
                }
                Assert.That(job.Status, Is.EqualTo(ScanJobState.Completed));
                Assert.That(job.Changes, Has.Count.EqualTo(3));
                FileChangeDto created = job.Changes!.Single(change => change.ChangeType == FileChangeKind.Created);
                FileChangeDto deleted = job.Changes!.Single(change => change.ChangeType == FileChangeKind.Deleted);
                FileChangeDto modified = job.Changes!.Single(change => change.ChangeType == FileChangeKind.Modified);
                Assert.Multiple(() =>
                {
                    Assert.That(created.Current!.RelativePath, Is.EqualTo("server-only.txt"));
                    Assert.That(deleted.Previous!.RelativePath, Is.EqualTo("client-only.txt"));
                    Assert.That(modified.Previous!.Size, Is.EqualTo(1));
                    Assert.That(modified.Current!.Size, Is.EqualTo(6));
                    Assert.That(File.ReadAllText(Path.Combine(local, "different.txt")), Is.EqualTo("x"));
                    Assert.That(Directory.GetFiles(local), Has.Length.EqualTo(3));
                    Assert.That(Directory.GetFiles(server), Has.Length.EqualTo(3));
                });
                MissingFileSynchronizer synchronizer = new();
                IReadOnlyList<FileCopyResult> copied = await synchronizer.CopyAsync(local,
                    [new HwSync.Abstractions.Models.FileSnapshot(created.Current!.RelativePath, created.Current.Size, created.Current.LastWriteTimeUtc)],
                    (file, output, token) => ((IFileDownloadClient)api).DownloadFileAsync(job.Id, file.RelativePath, output, token), timeout.Token);
                Assert.That(copied.Single().Copied, Is.True);
                Assert.That(File.ReadAllText(Path.Combine(local, "server-only.txt")), Is.EqualTo("server"));
                Assert.That(File.GetLastWriteTimeUtc(Path.Combine(local, "server-only.txt")), Is.EqualTo(created.Current.LastWriteTimeUtc));
                Assert.That(File.ReadAllText(Path.Combine(local, "different.txt")), Is.EqualTo("x"));
                Assert.That(File.Exists(Path.Combine(local, "client-only.txt")), Is.True);
                IFileMutationClient mutations = (IFileMutationClient)api;
                await mutations.EnsureServerFileMissingAsync(job.Id, "client-only.txt", timeout.Token);
                await using (FileStream input = File.OpenRead(Path.Combine(local, "client-only.txt")))
                {
                    await mutations.UploadFileAsync(job.Id, "client-only.txt", input, timeout.Token);
                }
                Assert.That(File.ReadAllText(Path.Combine(server, "client-only.txt")), Is.EqualTo("client"));
                Assert.ThrowsAsync<HwSyncApiException>(async () =>
                    await mutations.EnsureServerFileMissingAsync(job.Id, "client-only.txt", timeout.Token));
                await using (MemoryStream input = new([1, 2, 3]))
                {
                    Assert.ThrowsAsync<HwSyncApiException>(async () =>
                        await mutations.UploadFileAsync(job.Id, "client-only.txt", input, timeout.Token));
                }
                Assert.That(File.ReadAllText(Path.Combine(server, "client-only.txt")), Is.EqualTo("client"));
                Assert.ThrowsAsync<HwSyncApiException>(async () =>
                    await mutations.DeleteServerFileAsync(job.Id, "different.txt", timeout.Token));
                Assert.ThrowsAsync<HwSyncApiException>(async () =>
                    await mutations.DeleteServerFileAsync(job.Id, "../outside.txt", timeout.Token));
                await File.AppendAllTextAsync(Path.Combine(server, "server-only.txt"), "changed");
                Assert.ThrowsAsync<HwSyncApiException>(async () =>
                    await mutations.DeleteServerFileAsync(job.Id, "server-only.txt", timeout.Token));
                await File.WriteAllTextAsync(Path.Combine(server, "server-only.txt"), "server");
                File.SetLastWriteTimeUtc(Path.Combine(server, "server-only.txt"), created.Current.LastWriteTimeUtc);
                await mutations.DeleteServerFileAsync(job.Id, "server-only.txt", timeout.Token);
                Assert.That(File.Exists(Path.Combine(server, "server-only.txt")), Is.False);
                ScanJobResponse next = await api.StartComparisonAsync(new(server, snapshot), timeout.Token);
                while (next.FinishedAt is null)
                {
                    await Task.Delay(20, timeout.Token);
                    next = await api.GetScanAsync(next.Id, timeout.Token);
                }
                using HttpResponseMessage deletedResponse = await http.GetAsync(new Uri(new Uri(app.Urls.Single()), $"api/v1/scan-jobs/{next.Id}/deleted-files"), timeout.Token);
                deletedResponse.EnsureSuccessStatusCode();
                string deletedJson = await deletedResponse.Content.ReadAsStringAsync(timeout.Token);
                Assert.That(deletedJson, Does.Contain("server-only.txt"));
                Assert.That(deletedJson, Does.Contain("\"deleted\":true"));
                Assert.That(File.Exists(Path.Combine(local, "server-only.txt")), Is.True, "RecordOnly does not delete client files");
            }
            finally
            {
                await app.StopAsync(CancellationToken.None);
                foreach (string directory in new[]
{
 local, server
})
                {
                    foreach (string file in Directory.GetFiles(directory))
                    {
                        File.Delete(file);
                    }
                    Directory.Delete(directory);
                }
                Directory.Delete(root);
            }
        }
    }
}
