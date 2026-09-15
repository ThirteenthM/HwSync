using HwSync.Api.Contracts;
using HwSync.Api.Handlers;
using Microsoft.AspNetCore.Mvc;

namespace HwSync.Api.Controllers
{
    [ApiController]
    [Route("api/v1/scan-jobs")]
    public sealed class ScanJobsController : ControllerBase
    {
        private readonly ScanJobHandler _handler;

        public ScanJobsController(ScanJobHandler handler)
        {
            _handler = handler;
        }

        [HttpPost]
        public ActionResult<ScanJobResponse> Start(StartScanJobRequest request) => _handler.Start(request);

        [HttpGet("{id:guid}")]
        public ActionResult<ScanJobResponse> Get(Guid id) => _handler.Get(id);

        [HttpPost("{id:guid}/cancel")]
        public ActionResult<ScanJobResponse> Cancel(Guid id) => _handler.Cancel(id);
    }
}
