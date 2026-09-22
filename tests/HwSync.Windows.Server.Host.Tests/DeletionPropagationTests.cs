using HwSync.Api;
using HwSync.Api.Client;
using HwSync.Windows.AppServices.Client;
using HwSync.Windows.AppServices.Client.ViewModels;
using HwSync.Windows.Contract.Client.ViewModels;
using HwSync.Infrastructure.FileSystem;
using HwSync.Persistence.Sqlite;
using HwSync.Persistence.Sqlite.Migrations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace HwSync.Windows.Server.Host.Tests
{
    /// <summary>
    /// Распространение удалений между двумя клиентами через реальный HTTP и SQLite.
    /// </summary>
    public sealed class DeletionPropagationTests
    {
        /// <summary>
        /// Удаление из Проводника не восстанавливается; изменённая вторая копия требует решения.
        /// </summary>
        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        public async Task Deletion_ReachesSecondClient(bool removeOnServer, bool changeSecondClient)
        {
            string root = Path.Combine(Path.GetTempPath(), "HwSyncPropagation-" + Guid.NewGuid());
            string server = Path.Combine(root, "server");
            string first = Path.Combine(root, "first");
            string second = Path.Combine(root, "second");
            foreach (string directory in new[] { server, first, second })
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "file.txt"), "original");
                File.SetLastWriteTimeUtc(Path.Combine(directory, "file.txt"), DateTime.UnixEpoch);
            }

            WebApplicationBuilder builder = HostBootstrap.CreateBuilder(true, ["Urls=http://127.0.0.1:0"]);
            builder.Configuration["Storage:DatabasePath"] = Path.Combine(root, "metadata", "server.db");
            builder.Logging.ClearProviders();
            await using WebApplication app = builder.Build();
            app.MapHwSyncApi();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            try
            {
                await app.StartAsync(timeout.Token);
                using HttpClient http = new();
                HwSyncApiClient api = new(http, new(app.Urls.Single()));
                using MainViewModel client1 = CreateClient(root, first, server, "first", api);
                using MainViewModel client2 = CreateClient(root, second, server, "second", api);
                await client1.StartCommand.ExecuteAsync(null).WaitAsync(timeout.Token);
                await client2.StartCommand.ExecuteAsync(null).WaitAsync(timeout.Token);
                Assert.That(client1.Changes.Single().IsUnchanged, Is.True);
                Assert.That(client2.Changes.Single().IsUnchanged, Is.True);
                File.Delete(Path.Combine(removeOnServer ? server : first, "file.txt"));
                if (changeSecondClient)
                {
                    File.AppendAllText(Path.Combine(second, "file.txt"), " edited");
                }

                await client1.StartCommand.ExecuteAsync(null).WaitAsync(timeout.Token);
                Assert.That(client1.Changes.Single().Action,
                    Is.EqualTo(removeOnServer ? FileSyncDecision.DeleteOnClient : FileSyncDecision.DeleteOnServer));
                await client1.AutoSyncCommand.ExecuteAsync(null).WaitAsync(timeout.Token);
                Assert.That(client1.Error, Is.Empty);
                Assert.That(File.Exists(Path.Combine(server, "file.txt")), Is.False);
                Assert.That(File.Exists(Path.Combine(first, "file.txt")), Is.False);

                await client2.StartCommand.ExecuteAsync(null).WaitAsync(timeout.Token);
                Assert.That(client2.Error, Is.Empty);
                Assert.That(client2.Changes.Single().Action,
                    Is.EqualTo(changeSecondClient ? FileSyncDecision.AskUser : FileSyncDecision.DeleteOnClient));
                if (!changeSecondClient)
                {
                    await client2.AutoSyncCommand.ExecuteAsync(null).WaitAsync(timeout.Token);
                    Assert.That(client2.Error, Is.Empty);
                }

                Assert.That(File.Exists(Path.Combine(second, "file.txt")), Is.EqualTo(changeSecondClient));
                Assert.That(File.Exists(Path.Combine(server, "file.txt")), Is.False);
            }
            finally
            {
                await app.StopAsync(CancellationToken.None);
                foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    File.Delete(file);
                }

                foreach (string directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
                {
                    Directory.Delete(directory);
                }

                Directory.Delete(root);
            }
        }

        /// <summary>
        /// Создаёт участника со своей базой и записью успешных снимков.
        /// </summary>
        private static MainViewModel CreateClient(string root, string folder, string server, string name, HwSyncApiClient api)
        {
            SqliteDatabase database = new(Path.Combine(root, "metadata", name + ".db"));
            new SqliteMigrator(database).Apply();
            SqliteFolderHistory history = new(database);
            MainViewModel model = new(_ => api, new HistorySnapshotProvider(new DirectorySnapshotProvider(), history),
                new ComparedFileOperations(), new SourceFileReader(), new MissingFileSynchronizer(), new SyncDecisionService(),
                new ConflictFileOperations(new SourceFileReader()), history);
            model.LoadProfiles([new()
            {
                ClientRootPath = folder, ServerRootPath = server,
                Rules = new() { ClientOnlyFiles = HwSync.Windows.Contract.Client.Configuration.SyncRule.Copy }
            }]);
            model.ConfirmDeletion = (_, _) => true;
            return model;
        }
    }
}
