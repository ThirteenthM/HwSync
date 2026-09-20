using HwSync.Api.Contracts.Administration;

namespace HwSync.Api.Client
{
    /// <summary>
    /// Подключение пользователя управления, независимое от участника синхронизации.
    /// </summary>
    public interface IAdministrationApiClient : IDisposable
    {
        /// <summary>
        /// Читает разрешённые настройки сервера.
        /// </summary>
        Task<ServerSettingsDto> GetSettingsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Читает известные серверу папки.
        /// </summary>
        Task<IReadOnlyList<ServerFolderDto>> GetFoldersAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Читает страницу похоронной книги выбранной папки.
        /// </summary>
        Task<DeletionPageDto> GetDeletionsAsync(string folderId, long after, CancellationToken cancellationToken);
    }
}
