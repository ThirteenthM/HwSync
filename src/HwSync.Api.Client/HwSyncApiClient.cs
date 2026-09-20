using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HwSync.Api.Contracts;

namespace HwSync.Api.Client
{
    /// <summary>
    /// HTTP-клиент сравнения папок и передачи файлов.
    /// </summary>
    public sealed class HwSyncApiClient : IHwSyncApiClient, IFileDownloadClient, IFileMutationClient, IConflictFileClient, IDeletionHistoryClient
    {
        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
        private readonly HttpClient _httpClient;
        private readonly Uri _serverAddress;
        private readonly HttpClient _transferClient;
        private readonly TimeSpan _transferTimeout;

        /// <summary>
        /// Принимает HTTP-клиенты и интервал ожидания прогресса передачи.
        /// </summary>
        public HwSyncApiClient(HttpClient httpClient, Uri serverAddress, HttpClient? transferClient = null, TimeSpan? transferTimeout = null)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(serverAddress);
            if (!serverAddress.IsAbsoluteUri || serverAddress.Scheme is not ("http" or "https")
                || serverAddress.UserInfo.Length != 0 || serverAddress.Query.Length != 0 || serverAddress.Fragment.Length != 0)
            {
                throw new ArgumentException("Укажите HTTP(S)-адрес сервера без логина, параметров и фрагмента.", nameof(serverAddress));
            }
            _httpClient = httpClient;
            _transferClient = transferClient ?? httpClient;
            _transferTimeout = transferTimeout ?? TimeSpan.FromMinutes(30);
            if (_transferTimeout <= TimeSpan.Zero || _transferTimeout.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(transferTimeout));
            }
            _serverAddress = new(serverAddress.AbsoluteUri.TrimEnd('/') + "/");
        }

        /// <summary>
        /// Запрашивает готовность сервера.
        /// </summary>
        public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) =>
            SendAsync<HealthResponse>(HttpMethod.Get, "health", null, cancellationToken);

        /// <summary>
        /// Отправляет снимок клиента и запускает сравнение на сервере.
        /// </summary>
        public Task<ScanJobResponse> StartComparisonAsync(CompareFoldersRequest request, CancellationToken cancellationToken = default) =>
            SendAsync<ScanJobResponse>(HttpMethod.Post, "api/v1/scan-jobs", request, cancellationToken);

        /// <summary>
        /// Получает историю удалений папки выбранного сравнения.
        /// </summary>
        public async Task<IReadOnlyList<DeletedFileDto>> GetDeletedFilesAsync(Guid jobId, CancellationToken token) =>
            await SendAsync<DeletedFileDto[]>(HttpMethod.Get, $"api/v1/scan-jobs/{jobId}/deleted-files", null, token);
        /// <summary>
        /// Получает состояние и результат задания.
        /// </summary>
        public Task<ScanJobResponse> GetScanAsync(Guid id, CancellationToken cancellationToken = default) =>
            SendAsync<ScanJobResponse>(HttpMethod.Get, $"api/v1/scan-jobs/{id}", null, cancellationToken);

        /// <summary>
        /// Запрашивает отмену задания на сервере.
        /// </summary>
        public Task<ScanJobResponse> CancelScanAsync(Guid id, CancellationToken cancellationToken = default) =>
            SendAsync<ScanJobResponse>(HttpMethod.Post, $"api/v1/scan-jobs/{id}/cancel", null, cancellationToken);

        /// <summary>
        /// Скачивает файл из результата сравнения в переданный поток.
        /// </summary>
        public async Task DownloadFileAsync(Guid jobId, string relativePath, Stream destination, CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource transfer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            transfer.CancelAfter(_transferTimeout);
            cancellationToken = transfer.Token;
            using HttpRequestMessage request = new(HttpMethod.Get, new Uri(_serverAddress,
                $"api/v1/scan-jobs/{jobId}/file?relativePath={Uri.EscapeDataString(relativePath)}"));
            using HttpResponseMessage response = await _transferClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HwSyncApiException(response.StatusCode, $"Не удалось получить файл: HTTP {(int)response.StatusCode}. Повторите сравнение.");
            }
            using ProgressStream output = new(destination, () => transfer.CancelAfter(_transferTimeout));
            await response.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Передаёт отсутствующий на сервере файл без перезаписи.
        /// </summary>
        public Task UploadFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token) =>
            MutateAsync(HttpMethod.Put, jobId, "file", relativePath, source, token);

        /// <summary>
        /// Удаляет серверный файл из выбранного сравнения.
        /// </summary>
        public Task DeleteServerFileAsync(Guid jobId, string relativePath, CancellationToken token) =>
            MutateAsync(HttpMethod.Delete, jobId, "file", relativePath, null, token);

        /// <summary>
        /// Проверяет отсутствие файла на сервере перед локальным удалением.
        /// </summary>
        public Task EnsureServerFileMissingAsync(Guid jobId, string relativePath, CancellationToken token) =>
            MutateAsync(HttpMethod.Post, jobId, "verify-missing", relativePath, null, token);

        /// <summary>
        /// Заменяет серверную версию файла из выбранного сравнения.
        /// </summary>
        public Task ReplaceServerFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token) =>
            MutateAsync(HttpMethod.Put, jobId, "conflict/replace", relativePath, source, token);

        /// <summary>
        /// Сохраняет клиентскую версию отдельно от серверного оригинала.
        /// </summary>
        public Task PreserveClientFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token) =>
            MutateAsync(HttpMethod.Put, jobId, "conflict/preserve", relativePath, source, token);
        /// <summary>
        /// Выполняет файловый запрос с отдельным ожиданием прогресса загрузки.
        /// </summary>
        private async Task MutateAsync(HttpMethod method, Guid jobId, string endpoint, string relativePath, Stream? source, CancellationToken token)
        {
            using HttpRequestMessage request = new(method, new Uri(_serverAddress,
                $"api/v1/scan-jobs/{jobId}/{endpoint}?relativePath={Uri.EscapeDataString(relativePath)}"));
            using CancellationTokenSource transfer = CancellationTokenSource.CreateLinkedTokenSource(token);
            if (source is not null)
            {
                request.Content = new StreamContent(new ProgressStream(source, () => transfer.CancelAfter(_transferTimeout)));
                transfer.CancelAfter(_transferTimeout);
                token = transfer.Token;
            }
            HttpClient client = source is null ? _httpClient : _transferClient;
            using HttpResponseMessage response = await client.SendAsync(request, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HwSyncApiException(response.StatusCode, $"Операция с файлом не выполнена: HTTP {(int)response.StatusCode}. Повторите сравнение.");
            }
        }

        /// <summary>
        /// Выполняет запрос и преобразует ответ или ошибку API.
        /// </summary>
        private async Task<T> SendAsync<T>(HttpMethod method, string path, CompareFoldersRequest? body, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new(method, new Uri(_serverAddress, path));
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: JsonOptions);
            }
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                string message = $"Сервер вернул ошибку HTTP {(int)response.StatusCode}.";
                try
                {
                    JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken).ConfigureAwait(false);
                    if (problem.ValueKind == JsonValueKind.Object)
                    {
                        if (problem.TryGetProperty("detail", out JsonElement detail) && detail.ValueKind == JsonValueKind.String)
                        {
                            message += " " + detail.GetString();
                        }
                        else if (problem.TryGetProperty("title", out JsonElement title) && title.ValueKind == JsonValueKind.String)
                        {
                            message += " " + title.GetString();
                        }
                    }
                }
                catch (JsonException)
                {

                }
                throw new HwSyncApiException(response.StatusCode, message);
            }
            try
            {
                return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidDataException("Сервер вернул пустой ответ.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Ответ сервера не соответствует контракту HwSync.", exception);
            }
        }

        /// <summary>
        /// Настраивает JSON с текстовыми значениями перечислений.
        /// </summary>
        private static JsonSerializerOptions CreateJsonOptions()
        {
            JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}
