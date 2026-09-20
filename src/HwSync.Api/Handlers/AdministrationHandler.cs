using HwSync.Abstractions.Administration;
using HwSync.Api.Contracts.Administration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;

namespace HwSync.Api.Handlers
{
    /// <summary>
    /// Подготавливает сведения для утилиты администрирования.
    /// </summary>
    public sealed class AdministrationHandler
    {
        private readonly IAdministrationStore _store;
        private readonly IConfiguration _configuration;
        private readonly IServer _server;
        private readonly ServerStorageInfo _storage;

        /// <summary>
        /// Принимает источники состояния и разрешённых настроек.
        /// </summary>
        public AdministrationHandler(IAdministrationStore store, IConfiguration configuration, IServer server, ServerStorageInfo storage)
        {
            _store = store;
            _configuration = configuration;
            _server = server;
            _storage = storage;
        }

        /// <summary>
        /// Возвращает настройки без выгрузки всей конфигурации и секретов.
        /// </summary>
        public ServerSettingsDto GetSettings(string userName) => new(
            typeof(AdministrationHandler).Assembly.GetName().Version?.ToString() ?? "unknown",
            Environment.MachineName, _storage.DatabasePath,
            _server.Features.Get<IServerAddressesFeature>()?.Addresses.ToArray() ?? [],
            _configuration["Logging:LogLevel:Default"] ?? "Information", true, userName);

        /// <summary>
        /// Возвращает папки, о которых сервер сохранил снимки.
        /// </summary>
        public IReadOnlyList<ServerFolderDto> GetFolders() =>
            _store.GetFolders().Select(folder => new ServerFolderDto(
                folder.Id, folder.RootPath, folder.FileCount, folder.ActiveDeletionCount)).ToArray();

        /// <summary>
        /// Возвращает страницу журнала с курсором без полного чтения истории.
        /// </summary>
        public DeletionPageDto? GetDeletions(string folderId, long after, int limit)
        {
            IReadOnlyList<DeletionEntry>? entries = _store.GetDeletions(folderId, after, limit + 1);
            if (entries is null)
            {
                return null;
            }

            DeletionEntryDto[] page = entries.Take(limit).Select(entry => new DeletionEntryDto(
                entry.Number, entry.RelativePath, entry.OriginParticipantId, entry.DeletedAtUtc,
                entry.PreviousSize, entry.PreviousModifiedUtc, entry.Active)).ToArray();
            return new(page, entries.Count > limit ? page[^1].Number : null);
        }
    }
}
