using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using HwSync.Abstractions.Services;
using HwSync.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
namespace HwSync.Api.Handlers
{
    /// <summary>Выдача файлов и истории удалений завершённого задания.</summary>
    public sealed class FileTransferHandler
    {
        private readonly IScanJobService _jobs;
        private readonly ISourceFileReader _reader;
        private readonly IFolderHistory _history;

        /// <summary>Принимает задания, проверяемое чтение файлов и историю папок.</summary>
        public FileTransferHandler(IScanJobService jobs, ISourceFileReader reader, IFolderHistory history)
        {
            _jobs = jobs;
            _reader = reader;
            _history = history;
        }

        /// <summary>Возвращает поток файла из завершённого сравнения.</summary>
        public IActionResult Download(Guid id, string relativePath)
        {
            ScanJob? job = _jobs.Get(id);
            if (job is null)
            {
                return new NotFoundResult();
            }
            if (job.Status != ScanJobStatus.Completed || job.SourceRootPath is null)
            {
                return new ConflictResult();
            }
            // Выдаём только файлы из результата конкретного завершённого сравнения.
            FileSnapshot? file = job.Changes?.FirstOrDefault(change => change.Current?.RelativePath == relativePath)?.Current;
            if (file is null)
            {
                return new NotFoundResult();
            }
            try
            {
                return new FileStreamResult(_reader.OpenRead(job.SourceRootPath, file), "application/octet-stream");
            }
            catch (IOException exception)
            {
                return new ConflictObjectResult(new ProblemDetails
                {
                    Status = 409,
                    Detail = exception.Message
                });
            }
            catch (UnauthorizedAccessException)
            {
                return new StatusCodeResult(403);
            }
        }

        /// <summary>Возвращает историю удалений папки задания.</summary>
        public ActionResult<IReadOnlyList<DeletedFileDto>> GetDeleted(Guid id)
        {
            ScanJob? job = _jobs.Get(id);
            if (job is null)
            {
                return new NotFoundResult();
            }
            if (job.Status != ScanJobStatus.Completed || job.SourceRootPath is null)
            {
                return new ConflictResult();
            }
            return _history.GetDeletedFiles(job.SourceRootPath).Select(file => new DeletedFileDto(
                file.RelativePath, file.Deleted, file.DeletedAtUtc, file.ChangeNumber)).ToArray();
        }
    }
}
