using HwSync.Api.Contracts;
using HwSync.Api.Handlers;
using Microsoft.AspNetCore.Mvc;

namespace HwSync.Api.Controllers
{
    /// <summary>
    /// Маршруты сравнения папок и ручных файловых операций.
    /// </summary>
    [ApiController]
    [Route("api/v1/scan-jobs")]
    public sealed class ScanJobsController : ControllerBase
    {
        private readonly ScanJobHandler _handler;

        /// <summary>
        /// Принимает обработчик запросов к заданиям.
        /// </summary>
        public ScanJobsController(ScanJobHandler handler)
        {
            _handler = handler;
        }

        /// <summary>
        /// Передаёт выбранную клиентскую версию для замены серверной.
        /// </summary>
        [HttpPut("{id:guid}/conflict/replace")]
        [DisableRequestSizeLimit]
        public Task<IActionResult> ReplaceConflict(Guid id, string relativePath, [FromServices] ConflictFileHandler handler) =>
            handler.ExecuteAsync(id, relativePath, false, Request.Body, HttpContext.RequestAborted);

        /// <summary>
        /// Сохраняет обе версии, принимая клиентскую под отдельным именем.
        /// </summary>
        [HttpPut("{id:guid}/conflict/preserve")]
        [DisableRequestSizeLimit]
        public Task<IActionResult> PreserveConflict(Guid id, string relativePath, [FromServices] ConflictFileHandler handler) =>
            handler.ExecuteAsync(id, relativePath, true, Request.Body, HttpContext.RequestAborted);
        /// <summary>
        /// Передаёт поток запроса обработчику загрузки файла.
        /// </summary>
        [HttpPut("{id:guid}/file")]
        [DisableRequestSizeLimit]
        public Task<IActionResult> Upload(Guid id, string relativePath, [FromServices] FileMutationHandler handler) =>
            handler.ExecuteAsync(id, relativePath, FileMutationOperation.Upload, Request.Body, HttpContext.RequestAborted);

        /// <summary>
        /// Передаёт обработчику запрос удаления серверного файла.
        /// </summary>
        [HttpDelete("{id:guid}/file")]
        public Task<IActionResult> Delete(Guid id, string relativePath, [FromServices] FileMutationHandler handler) =>
            handler.ExecuteAsync(id, relativePath, FileMutationOperation.Delete, Stream.Null, HttpContext.RequestAborted);

        /// <summary>
        /// Проверяет отсутствие серверной копии перед удалением на клиенте.
        /// </summary>
        [HttpPost("{id:guid}/verify-missing")]
        public Task<IActionResult> VerifyMissing(Guid id, string relativePath, [FromServices] FileMutationHandler handler) =>
            handler.ExecuteAsync(id, relativePath, FileMutationOperation.VerifyMissing, Stream.Null, HttpContext.RequestAborted);

        /// <summary>
        /// Возвращает поток файла из завершённого сравнения.
        /// </summary>
        [HttpGet("{id:guid}/file")]
        public IActionResult Download(Guid id, string relativePath, [FromServices] FileTransferHandler handler) => handler.Download(id, relativePath);

        /// <summary>
        /// Возвращает отметки удаления файлов папки.
        /// </summary>
        [HttpGet("{id:guid}/deleted-files")]
        public ActionResult<IReadOnlyList<DeletedFileDto>> DeletedFiles(Guid id, [FromServices] FileTransferHandler handler) => handler.GetDeleted(id);

        /// <summary>
        /// Ставит сравнение папки в очередь.
        /// </summary>
        [HttpPost]
        public ActionResult<ScanJobResponse> Start(CompareFoldersRequest request) => _handler.Start(request);

        /// <summary>
        /// Возвращает состояние задания по идентификатору.
        /// </summary>
        [HttpGet("{id:guid}")]
        public ActionResult<ScanJobResponse> Get(Guid id) => _handler.Get(id);

        /// <summary>
        /// Запрашивает отмену задания по идентификатору.
        /// </summary>
        [HttpPost("{id:guid}/cancel")]
        public ActionResult<ScanJobResponse> Cancel(Guid id) => _handler.Cancel(id);
    }
}
