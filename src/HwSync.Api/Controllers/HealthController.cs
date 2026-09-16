using HwSync.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HwSync.Api.Controllers
{
    [ApiController]
    [Route("health")]
    public sealed class HealthController : ControllerBase
    {
        // MVC публикует только методы экземпляра: этот метод нельзя делать static.
        [HttpGet]
        public HealthResponse Get() => new("ok");
    }
}
