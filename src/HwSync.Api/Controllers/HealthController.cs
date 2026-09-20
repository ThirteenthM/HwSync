using HwSync.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HwSync.Api.Controllers
{
    /// <summary>
    /// HTTP-проверка готовности сервера.
    /// </summary>
    [ApiController]
    [Route("health")]
    public sealed class HealthController : ControllerBase
    {
        // MVC публикует только методы экземпляра: этот метод нельзя делать static.
        /// <summary>
        /// Возвращает подтверждение готовности сервера.
        /// </summary>
        [HttpGet]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Performance",
            "CA1822:Mark members as static",
            Justification = "MVC требует метод экземпляра для действия контроллера.")]
        public HealthResponse Get() => new("ok");
    }
}
