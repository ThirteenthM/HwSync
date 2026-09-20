using System.Net.Http.Headers;
using System.Net.Http.Json;
using HwSync.Api.Contracts.Administration;

namespace HwSync.Api.Client
{
    /// <summary>
    /// Авторизованный HTTP-доступ к просмотру серверных настроек и истории.
    /// </summary>
    public sealed class AdministrationApiClient : IAdministrationApiClient
    {
        private readonly HttpClient _http;
        private readonly Uri _server;
        private readonly string _accessToken;

        /// <summary>
        /// Принимает во владение HTTP-подключение без автоматических перенаправлений.
        /// </summary>
        public AdministrationApiClient(HttpClient http, Uri server, string accessToken)
        {
            _http = http;
            _server = server;
            _accessToken = accessToken;
        }

        /// <summary>
        /// Читает разрешённые настройки сервера.
        /// </summary>
        public Task<ServerSettingsDto> GetSettingsAsync(CancellationToken cancellationToken) =>
            GetAsync<ServerSettingsDto>("api/v1/admin/settings", cancellationToken);

        /// <summary>
        /// Читает каталог известных серверу папок.
        /// </summary>
        public async Task<IReadOnlyList<ServerFolderDto>> GetFoldersAsync(CancellationToken cancellationToken) =>
            await GetAsync<ServerFolderDto[]>("api/v1/admin/folders", cancellationToken);

        /// <summary>
        /// Читает очередную страницу журнала без обращения к файлам базы.
        /// </summary>
        public Task<DeletionPageDto> GetDeletionsAsync(string folderId, long after, CancellationToken cancellationToken) =>
            GetAsync<DeletionPageDto>($"api/v1/admin/folders/{Uri.EscapeDataString(folderId)}/deletions?after={after}&limit=100", cancellationToken);

        /// <summary>
        /// Освобождает HTTP-подключение при закрытии или смене сервера.
        /// </summary>
        public void Dispose() => _http.Dispose();

        /// <summary>
        /// Передаёт ключ только в заголовке и проверяет HTTP-ответ перед чтением JSON.
        /// </summary>
        private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, new Uri(_server, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string message = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => "Ключ доступа не принят сервером.",
                    System.Net.HttpStatusCode.Forbidden => "Нет права просмотра сервера или подключение не локальное.",
                    _ => $"Сервер вернул {(int)response.StatusCode} ({response.ReasonPhrase})."
                };
                throw new HwSyncApiException(response.StatusCode, message);
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("Сервер вернул пустой ответ.");
        }
    }
}
