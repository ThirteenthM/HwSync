using System.Net;
using System.Security.Cryptography;
using System.Text;
using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using HwSync.Api;
using HwSync.Api.Client;
using HwSync.Api.Contracts.Administration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HwSync.Windows.Server.Host.Tests
{
    /// <summary>
    /// Проверки административного API против настоящего HTTP и SQLite.
    /// </summary>
    public class AdministrationApiTests
    {
        /// <summary>
        /// Без ключа, с неверным ключом и без права чтения сервер не раскрывает данные.
        /// </summary>
        [Test]
        public async Task Administration_RequiresSeparateUserAndReadPermission()
        {
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            WebApplicationBuilder builder = CreateBuilder();
            builder.Configuration["Administration:Users:0:Name"] = "reader";
            builder.Configuration["Administration:Users:0:TokenSha256"] = Hash(token);
            builder.Configuration["Administration:Users:0:CanRead"] = "false";
            await using WebApplication app = builder.Build();
            app.MapHwSyncApi();
            await app.StartAsync();
            try
            {
                using HttpClient http = new() { BaseAddress = new Uri(app.Urls.Single()) };
                foreach (string endpoint in new[] { "settings", "folders", "folders/unknown/deletions" })
                {
                    using HttpResponseMessage anonymous = await http.GetAsync("api/v1/admin/" + endpoint);
                    Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                    http.DefaultRequestHeaders.Authorization = new("Bearer", new string('z', 64));
                    using HttpResponseMessage invalid = await http.GetAsync("api/v1/admin/" + endpoint);
                    Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                    http.DefaultRequestHeaders.Authorization = new("Bearer", token);
                    using HttpResponseMessage forbidden = await http.GetAsync("api/v1/admin/" + endpoint);
                    Assert.That(forbidden.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                    http.DefaultRequestHeaders.Authorization = null;
                }

                using HttpResponseMessage health = await http.GetAsync("health");
                Assert.That(health.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            }
            finally
            {
                await app.StopAsync();
            }
        }

        /// <summary>
        /// Настройки и история доступны по праву чтения, страницы не теряют события.
        /// </summary>
        [Test]
        public async Task Administration_ReadsSettingsFoldersAndPagedHistory()
        {
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            WebApplicationBuilder builder = CreateBuilder();
            builder.Configuration["Administration:Users:0:Name"] = "operator";
            builder.Configuration["Administration:Users:0:TokenSha256"] = Hash(token);
            builder.Configuration["Administration:Users:0:CanRead"] = "true";
            builder.Configuration["SecretValue"] = "must-not-be-returned";
            await using WebApplication app = builder.Build();
            app.MapHwSyncApi();
            await app.StartAsync();
            try
            {
                IFolderHistory history = app.Services.GetRequiredService<IFolderHistory>();
                string root = Path.Combine(Path.GetTempPath(), "HwSync-Admin-" + Guid.NewGuid());
                DateTime time = new(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);
                FileSnapshot[] files = Enumerable.Range(1, 103).Select(i => new FileSnapshot($"file-{i}.txt", 1_234_567, time)).ToArray();
                history.RecordSnapshot(root, files);
                history.RecordSnapshot(root, []);
                history.RecordSnapshot(root, [files[0]]);
                history.RecordSnapshot(root + "-empty", []);

                using HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
                using IAdministrationApiClient api = new AdministrationApiClient(http, http.BaseAddress, token);
                ServerSettingsDto settings = await api.GetSettingsAsync(default);
                Assert.That(settings.UserName, Is.EqualTo("operator"));
                Assert.That(settings.DatabasePath, Is.EqualTo(builder.Configuration["Storage:DatabasePath"]));
                Assert.That(settings.LocalConnectionsOnly, Is.True);
                IReadOnlyList<ServerFolderDto> folders = await api.GetFoldersAsync(default);
                ServerFolderDto folder = folders.Single(item => item.RootPath.Equals(root, StringComparison.OrdinalIgnoreCase));
                Assert.That(folder.FileCount, Is.EqualTo(1));
                Assert.That(folder.ActiveDeletionCount, Is.EqualTo(102));
                DeletionPageDto first = await api.GetDeletionsAsync(folder.Id, 0, default);
                Assert.That(first.Entries, Has.Count.EqualTo(100));
                Assert.That(first.Entries[0].Active, Is.False);
                Assert.That(first.Entries[0].PreviousSize, Is.EqualTo(1_234_567));
                Assert.That(first.Entries[0].OriginParticipantId, Is.Not.Empty);
                DeletionPageDto second = await api.GetDeletionsAsync(folder.Id, first.NextCursor!.Value, default);
                Assert.That(second.Entries, Has.Count.EqualTo(3));
                Assert.That(second.NextCursor, Is.Null);
                Assert.That(first.Entries.Concat(second.Entries).Select(entry => entry.Number).Distinct().Count(), Is.EqualTo(103));
                ServerFolderDto empty = folders.Single(item => item.Id != folder.Id);
                Assert.That((await api.GetDeletionsAsync(empty.Id, 0, default)).Entries, Is.Empty);

                http.DefaultRequestHeaders.Authorization = new("Bearer", token);
                using HttpResponseMessage missing = await http.GetAsync("api/v1/admin/folders/unknown/deletions");
                Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                foreach (string query in new[] { "after=-1", "limit=0", "limit=501" })
                {
                    using HttpResponseMessage invalid = await http.GetAsync($"api/v1/admin/folders/{folder.Id}/deletions?{query}");
                    Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                }

                string json = await http.GetStringAsync("api/v1/admin/settings");
                Assert.That(json, Does.Not.Contain("must-not-be-returned").And.Not.Contain(token).And.Not.Contain(Hash(token)));
            }
            finally
            {
                await app.StopAsync();
            }
        }

        /// <summary>
        /// Без настроенных пользователей административный доступ закрыт.
        /// </summary>
        [Test]
        public async Task Administration_NoUsersFailsClosed()
        {
            WebApplicationBuilder builder = CreateBuilder();
            await using WebApplication app = builder.Build();
            app.MapHwSyncApi();
            await app.StartAsync();
            try
            {
                using HttpClient http = new() { BaseAddress = new Uri(app.Urls.Single()) };
                http.DefaultRequestHeaders.Authorization = new("Bearer", new string('a', 64));
                using HttpResponseMessage response = await http.GetAsync("api/v1/admin/settings");
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            }
            finally
            {
                await app.StopAsync();
            }
        }

        /// <summary>
        /// Настраивает изолированную базу и случайный локальный порт.
        /// </summary>
        private static WebApplicationBuilder CreateBuilder()
        {
            WebApplicationBuilder builder = HostBootstrap.CreateBuilder(true, ["Urls=http://127.0.0.1:0"]);
            builder.Configuration["Storage:DatabasePath"] = Path.Combine(TestContext.CurrentContext.WorkDirectory,
                "artifacts", "admin-tests", Guid.NewGuid().ToString("N"), "state.db");
            builder.Logging.ClearProviders();
            return builder;
        }

        /// <summary>
        /// Вычисляет хеш тестового персонального ключа.
        /// </summary>
        private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
