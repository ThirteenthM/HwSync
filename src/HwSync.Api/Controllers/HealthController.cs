using HwSync.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HwSync.Api.Controllers
{
    /// <summary>HTTP-проверка готовности сервера.</summary>
    [ApiController]
    [Route("health")]
    public sealed class HealthController : ControllerBase
    {
        // MVC публикует только методы экземпляра: этот метод нельзя делать static.
        /// <summary>Возвращает подтверждение готовности сервера.</summary>
        [HttpGet]
        public HealthResponse Get() => new("ok");
    }
}
