using HwSync.Abstractions.Models;
using HwSync.Abstractions.Services;
using HwSync.Persistence.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HwSync.Server.AppServices.Tests
{
    /// <summary>
    /// Запуск общих серверных служб без ссылки на платформенный Host.
    /// </summary>
    public class ServerServicesTests
    {
        /// <summary>
        /// Миграции и обработка задания работают с путём Host или переопределением конфигурации.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task SharedServices_StartDatabaseAndProcessJob(bool overrideDatabasePath)
        {
            string directory = CreateDirectory();
            string root = Path.Combine(directory, "files");
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "sample.txt"), "sample");
            string defaultPath = Path.Combine(directory, "default.db");
            string expectedPath = overrideDatabasePath ? Path.Combine(directory, "configured.db") : defaultPath;
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Configuration["Storage:DatabasePath"] = overrideDatabasePath ? expectedPath : "";
            builder.Services.AddServerAppServices(builder.Configuration, defaultPath);
            await using WebApplication app = builder.Build();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(15));
            await app.StartAsync(timeout.Token);
            try
            {
                Assert.That(app.Services.GetRequiredService<SqliteDatabase>().FilePath, Is.EqualTo(expectedPath));
                using (SqliteConnection connection = app.Services.GetRequiredService<SqliteDatabase>().OpenConnection())
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT count(*) FROM schema_migrations";
                    Assert.That(command.ExecuteScalar(), Is.EqualTo(1));
                }

                IScanJobService jobs = app.Services.GetRequiredService<IScanJobService>();
                ScanJob job = jobs.Start(new(root, []));
                while (job.FinishedAt is null)
                {
                    await Task.Delay(20, timeout.Token);
                    job = jobs.Get(job.Id)!;
                }

                Assert.That(job.Status, Is.EqualTo(ScanJobStatus.Completed));
                Assert.That(job.Changes, Has.Count.EqualTo(1));
                Assert.That(job.Error, Is.Null);
                if (overrideDatabasePath)
                {
                    Assert.That(File.Exists(defaultPath), Is.False);
                }
            }
            finally
            {
                await app.StopAsync(CancellationToken.None);
            }
        }

        /// <summary>
        /// Повреждённая база не позволяет запустить общий серверный слой.
        /// </summary>
        [Test]
        public async Task SharedServices_InvalidDatabasePreventsStartup()
        {
            string databasePath = Path.Combine(CreateDirectory(), "broken.db");
            await File.WriteAllTextAsync(databasePath, "not a database");
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseSetting("urls", "http://127.0.0.1:0");
            builder.Configuration["Storage:DatabasePath"] = databasePath;
            builder.Logging.ClearProviders();
            builder.Services.AddServerAppServices(builder.Configuration, databasePath);
            await using WebApplication app = builder.Build();
            try
            {
                Assert.ThrowsAsync<SqliteException>(() => app.StartAsync());
                Assert.That(app.Lifetime.ApplicationStarted.IsCancellationRequested, Is.False);
            }
            finally
            {
                await app.StopAsync();
            }
        }

        /// <summary>
        /// Создаёт собственный каталог каждого теста.
        /// </summary>
        private static string CreateDirectory()
        {
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "server-services", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
