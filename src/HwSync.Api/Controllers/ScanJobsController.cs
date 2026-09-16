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

        [HttpGet("{id:guid}/file")]
        public IActionResult Download(Guid id, string relativePath, [FromServices] FileTransferHandler handler) => handler.Download(id, relativePath);

        [HttpGet("{id:guid}/deleted-files")]
        public ActionResult<IReadOnlyList<DeletedFileDto>> DeletedFiles(Guid id, [FromServices] FileTransferHandler handler) => handler.GetDeleted(id);

        [HttpPost]
        public ActionResult<ScanJobResponse> Start(CompareFoldersRequest request) => _handler.Start(request);

        [HttpGet("{id:guid}")]
        public ActionResult<ScanJobResponse> Get(Guid id) => _handler.Get(id);

        [HttpPost("{id:guid}/cancel")]
        public ActionResult<ScanJobResponse> Cancel(Guid id) => _handler.Cancel(id);
    }
}
