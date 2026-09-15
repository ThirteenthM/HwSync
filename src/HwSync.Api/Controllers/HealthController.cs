using HwSync.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HwSync.Api.Controllers
{
    [ApiController]
    [Route("health")]
    public sealed class HealthController : ControllerBase
    {
        [HttpGet]
        public static HealthResponse Get() => new(@"ok - {DateTime.Now}");
    }
}
