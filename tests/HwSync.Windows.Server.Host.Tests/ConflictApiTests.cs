using HwSync.Abstractions.Models;
using HwSync.Api;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Infrastructure.FileSystem;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace HwSync.Windows.Server.Host.Tests
{
    /// <summary>
    /// Проверки конфликтных операций через настоящий HTTP.
    /// </summary>
    public sealed class ConflictApiTests
    {
        /// <summary>
        /// Проверяет актуальность версии и удаляет только файл из сравнения.
        /// </summary>
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, true)]
        [TestCase(false, false, false, true)]
        [TestCase(true, false, false, true)]
        [TestCase(false, true, false, true)]
        [TestCase(true, true, false, true)]
        [TestCase(true, false, true, true)]
        [TestCase(true, true, true, true)]
        public async Task ComparedFile_RequiresUnchangedVersion(bool delete, bool changed, bool equal = false, bool differentCase = false)
        {
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "compared-api", Guid.NewGuid().ToString("N"));
            string server = Path.Combine(root, "server");
            Directory.CreateDirectory(server);
            string serverName = differentCase ? "File.txt" : "file.txt";
            string requestPath = differentCase ? "FILE.TXT" : "file.txt";
            string target = Path.Combine(server, serverName);
            await File.WriteAllTextAsync(target, "server");
            WebApplicationBuilder builder = HostBootstrap.CreateBuilder(true, ["Urls=http://127.0.0.1:0"]);
            builder.Configuration["Storage:DatabasePath"] = Path.Combine(root, "state.db");
            builder.Configuration["Storage:AllowedRoots:0"] = server;
            builder.Logging.ClearProviders();
            await using WebApplication app = builder.Build();
            app.MapHwSyncApi();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
            try
            {
                await app.StartAsync(timeout.Token);
                using HttpClient http = new();
                HwSyncApiClient api = new(http, new(app.Urls.Single()));
                ScanJobResponse job = await api.StartComparisonAsync(new(server,
                    [new("file.txt", equal ? 6 : 10, equal ? File.GetLastWriteTimeUtc(target) : DateTime.UtcNow.AddMinutes(-5))]), timeout.Token);
                while (job.FinishedAt is null)
                {
                    await Task.Delay(20, timeout.Token);
                    job = await api.GetScanAsync(job.Id, timeout.Token);
                }

                Assert.That(job.Changes!.Single().ChangeType, Is.EqualTo(equal ? FileChangeKind.Unchanged : FileChangeKind.Modified));
                Assert.ThrowsAsync<HwSyncApiException>(async () =>
                    await api.DeleteComparedServerFileAsync(job.Id, "../outside.txt", timeout.Token));
                if (changed)
                {
                    await File.WriteAllTextAsync(target, "modified server");
                }

                Task action = delete
                    ? api.DeleteComparedServerFileAsync(job.Id, requestPath, timeout.Token)
                    : api.EnsureServerFileUnchangedAsync(job.Id, requestPath, timeout.Token);
                if (changed)
                {
                    HwSyncApiException? error = Assert.ThrowsAsync<HwSyncApiException>(async () => await action);
                    Assert.That(error!.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Conflict));
                    Assert.That(File.ReadAllText(target), Is.EqualTo("modified server"));
                }
                else
                {
                    await action;
                    Assert.That(File.Exists(target), Is.EqualTo(!delete));
                }
            }
            finally
            {
                await app.StopAsync(CancellationToken.None);
            }
        }
        /// <summary>
        /// Заменяет серверную версию либо сохраняет обе, отклоняя устаревший снимок.
        /// </summary>
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(false, false, true)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, true, true)]
        public async Task Conflict_UsesComparisonPreconditions(bool preserve, bool changed, bool differentCase = false)
        {
            string root = Path.Combine(Path.GetTempPath(), "HwSyncConflictApi-" + Guid.NewGuid());
            string server = Path.Combine(root, "server");
            Directory.CreateDirectory(server);
            string serverName = differentCase ? "File.txt" : "file.txt";
            string requestPath = differentCase ? "FILE.TXT" : "file.txt";
            string target = Path.Combine(server, serverName);
            await File.WriteAllTextAsync(target, "server");
            DateTime clientTime = DateTime.UtcNow.AddMinutes(-5);
            WebApplicationBuilder builder = HostBootstrap.CreateBuilder(true, ["Urls=http://127.0.0.1:0"]);
            builder.Configuration["Storage:DatabasePath"] = Path.Combine(root, "history", "state.db");
            builder.Configuration["Storage:AllowedRoots:0"] = server;
            builder.Logging.ClearProviders();
            await using WebApplication app = builder.Build();
            app.MapHwSyncApi();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(20));
            try
            {
                await app.StartAsync(timeout.Token);
                using HttpClient http = new();
                HwSyncApiClient api = new(http, new(app.Urls.Single()));
                ScanJobResponse job = await api.StartComparisonAsync(new(server,
                    [new("file.txt", 6, clientTime)]), timeout.Token);
                while (job.FinishedAt is null)
                {
                    await Task.Delay(20, timeout.Token);
                    job = await api.GetScanAsync(job.Id, timeout.Token);
                }

                FileChangeDto difference = job.Changes!.Single();
                Assert.That(difference.ChangeType, Is.EqualTo(FileChangeKind.Modified));
                Assert.That(difference.Previous!.RelativePath, Is.EqualTo("file.txt"));
                Assert.That(difference.Current!.RelativePath, Is.EqualTo(serverName));
                using MemoryStream downloaded = new();
                await api.DownloadFileAsync(job.Id, requestPath, downloaded, timeout.Token);
                Assert.That(System.Text.Encoding.UTF8.GetString(downloaded.ToArray()), Is.EqualTo("server"));
                if (changed)
                {
                    await File.WriteAllTextAsync(target, "server changed");
                }

                using MemoryStream source = new("client"u8.ToArray());
                Task action = preserve
                    ? api.PreserveClientFileAsync(job.Id, requestPath, source, timeout.Token)
                    : api.ReplaceServerFileAsync(job.Id, requestPath, source, timeout.Token);
                string backup = Path.Combine(server, ConflictCopyPath.Create("file.txt", job.Id));
                if (changed)
                {
                    HwSyncApiException? error = Assert.ThrowsAsync<HwSyncApiException>(async () => await action);
                    Assert.That(error!.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Conflict));
                    Assert.That(File.ReadAllText(target), Is.EqualTo("server changed"));
                    Assert.That(File.Exists(backup), Is.False);
                }
                else
                {
                    await action;
                    Assert.That(File.ReadAllText(target), Is.EqualTo(preserve ? "server" : "client"));
                    Assert.That(File.Exists(backup), Is.EqualTo(preserve));
                    if (preserve)
                    {
                        Assert.That(File.ReadAllText(backup), Is.EqualTo("client"));
                        Assert.That(Path.GetFileName(Directory.GetFiles(server, "*client-conflict*").Single()),
                            Is.EqualTo(ConflictCopyPath.Create("file.txt", job.Id)));
                    }

                    using MemoryStream repeated = new("client"u8.ToArray());
                    Assert.ThrowsAsync<HwSyncApiException>(async () =>
                    {
                        if (preserve)
                        {
                            await api.PreserveClientFileAsync(job.Id, requestPath, repeated, timeout.Token);
                        }
                        else
                        {
                            await api.ReplaceServerFileAsync(job.Id, requestPath, repeated, timeout.Token);
                        }
                    });
                }

                using MemoryStream invalid = new("client"u8.ToArray());
                Assert.ThrowsAsync<HwSyncApiException>(async () =>
                    await api.ReplaceServerFileAsync(job.Id, "../escape.txt", invalid, timeout.Token));
            }
            finally
            {
                await app.StopAsync(CancellationToken.None);
                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    File.Delete(file);
                }

                foreach (string directory in Directory.GetDirectories(root).OrderByDescending(path => path.Length))
                {
                    Directory.Delete(directory);
                }

                Directory.Delete(root);
            }
        }
    }
}
