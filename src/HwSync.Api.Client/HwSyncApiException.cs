using System.Net;

namespace HwSync.Api.Client
{
    /// <summary>Ошибка HTTP API с сохранением кода ответа.</summary>
    public sealed class HwSyncApiException : HttpRequestException
    {
        /// <summary>Сохраняет HTTP-статус и сообщение сервера.</summary>
        public HwSyncApiException(HttpStatusCode statusCode, string message)
            : base(message, null, statusCode)
        {
        }
    }
}
