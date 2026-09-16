using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HwSync.Api.Contracts;

namespace HwSync.Api.Client
{
    public sealed class HwSyncApiClient : IHwSyncApiClient, IFileDownloadClient
    {
        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
        private readonly HttpClient _httpClient;
        private readonly Uri _serverAddress;

        public HwSyncApiClient(HttpClient httpClient, Uri serverAddress)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(serverAddress);
            if (!serverAddress.IsAbsoluteUri || serverAddress.Scheme is not ("http" or "https")
                || serverAddress.UserInfo.Length != 0 || serverAddress.Query.Length != 0 || serverAddress.Fragment.Length != 0)
            {
                throw new ArgumentException("Укажите HTTP(S)-адрес сервера без логина, параметров и фрагмента.", nameof(serverAddress));
            }
            _httpClient = httpClient;
            _serverAddress = new(serverAddress.AbsoluteUri.TrimEnd('/') + "/");
        }

        public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) =>
            SendAsync<HealthResponse>(HttpMethod.Get, "health", null, cancellationToken);

        public Task<ScanJobResponse> StartComparisonAsync(CompareFoldersRequest request, CancellationToken cancellationToken = default) =>
            SendAsync<ScanJobResponse>(HttpMethod.Post, "api/v1/scan-jobs", request, cancellationToken);

        public Task<ScanJobResponse> GetScanAsync(Guid id, CancellationToken cancellationToken = default) =>
            SendAsync<ScanJobResponse>(HttpMethod.Get, $"api/v1/scan-jobs/{id}", null, cancellationToken);

        public Task<ScanJobResponse> CancelScanAsync(Guid id, CancellationToken cancellationToken = default) =>
            SendAsync<ScanJobResponse>(HttpMethod.Post, $"api/v1/scan-jobs/{id}/cancel", null, cancellationToken);

        public async Task DownloadFileAsync(Guid jobId, string relativePath, Stream destination, CancellationToken cancellationToken = default)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, new Uri(_serverAddress,
                $"api/v1/scan-jobs/{jobId}/file?relativePath={Uri.EscapeDataString(relativePath)}"));
            using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            { throw new HwSyncApiException(response.StatusCode, $"Не удалось получить файл: HTTP {(int)response.StatusCode}. Повторите сравнение."); }
            await response.Content.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }
        private async Task<T> SendAsync<T>(HttpMethod method, string path, CompareFoldersRequest? body, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new(method, new Uri(_serverAddress, path));
            if (body is not null) { request.Content = JsonContent.Create(body, options: JsonOptions); }
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
                catch (JsonException) { }
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

        private static JsonSerializerOptions CreateJsonOptions()
        {
            JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }
    }
}
