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

        [HttpPut("{id:guid}/file")]
        [DisableRequestSizeLimit]
        public Task<IActionResult> Upload(Guid id, string relativePath, [FromServices] FileMutationHandler handler) =>
            handler.ExecuteAsync(id, relativePath, "upload", Request.Body, HttpContext.RequestAborted);

        [HttpDelete("{id:guid}/file")]
        public Task<IActionResult> Delete(Guid id, string relativePath, [FromServices] FileMutationHandler handler) =>
            handler.ExecuteAsync(id, relativePath, "delete", Stream.Null, HttpContext.RequestAborted);

        [HttpPost("{id:guid}/verify-missing")]
        public Task<IActionResult> VerifyMissing(Guid id, string relativePath, [FromServices] FileMutationHandler handler) =>
            handler.ExecuteAsync(id, relativePath, "verify", Stream.Null, HttpContext.RequestAborted);
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
