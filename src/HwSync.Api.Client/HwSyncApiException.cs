using System.Net;

namespace HwSync.Api.Client
{
    public sealed class HwSyncApiException : HttpRequestException
    {
        public HwSyncApiException(HttpStatusCode statusCode, string message)
            : base(message, null, statusCode)
        {
        }
    }
}
