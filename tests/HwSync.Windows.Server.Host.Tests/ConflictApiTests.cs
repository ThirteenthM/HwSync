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
        /// Заменяет серверную версию либо сохраняет обе, отклоняя устаревший снимок.
        /// </summary>
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public async Task Conflict_UsesComparisonPreconditions(bool preserve, bool changed)
        {
            string root = Path.Combine(Path.GetTempPath(), "HwSyncConflictApi-" + Guid.NewGuid());
            string server = Path.Combine(root, "server");
            Directory.CreateDirectory(server);
            string target = Path.Combine(server, "file.txt");
            await File.WriteAllTextAsync(target, "server");
            DateTime clientTime = DateTime.UtcNow.AddMinutes(-5);
            WebApplicationBuilder builder = HostBootstrap.CreateBuilder(true, ["Urls=http://127.0.0.1:0"]);
            builder.Configuration["Storage:DatabasePath"] = Path.Combine(root, "history", "state.db");
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

                Assert.That(job.Changes!.Single().ChangeType, Is.EqualTo(FileChangeKind.Modified));
                if (changed)
                {
                    await File.WriteAllTextAsync(target, "server changed");
                }

                using MemoryStream source = new("client"u8.ToArray());
                Task action = preserve
                    ? api.PreserveClientFileAsync(job.Id, "file.txt", source, timeout.Token)
                    : api.ReplaceServerFileAsync(job.Id, "file.txt", source, timeout.Token);
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
                    }

                    using MemoryStream repeated = new("client"u8.ToArray());
                    Assert.ThrowsAsync<HwSyncApiException>(async () =>
                    {
                        if (preserve)
                        {
                            await api.PreserveClientFileAsync(job.Id, "file.txt", repeated, timeout.Token);
                        }
                        else
                        {
                            await api.ReplaceServerFileAsync(job.Id, "file.txt", repeated, timeout.Token);
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
