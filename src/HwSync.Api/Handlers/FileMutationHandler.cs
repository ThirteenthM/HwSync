using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using HwSync.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;
namespace HwSync.Api.Handlers
{
    /// <summary>
    /// Проверка и выполнение файловых изменений через API.
    /// </summary>
    public sealed class FileMutationHandler
    {
        private readonly IScanJobService _jobs;
        private readonly IComparedFileOperations _files;
        private readonly ISourceFileReader _reader;

        /// <summary>
        /// Принимает хранилище заданий и файловые операции.
        /// </summary>
        public FileMutationHandler(IScanJobService jobs, IComparedFileOperations files, ISourceFileReader reader)
        {
            _jobs = jobs;
            _files = files;
            _reader = reader;
        }

        /// <summary>
        /// Выполняет действие только для подходящего файла завершённого сравнения.
        /// </summary>
        public async Task<IActionResult> ExecuteAsync(Guid id, string relativePath, FileMutationOperation operation, Stream body, CancellationToken token)
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

            FileChangeType kind = operation switch
            {
                FileMutationOperation.Delete => FileChangeType.Created,
                FileMutationOperation.DeleteCompared or FileMutationOperation.VerifyUnchanged => FileChangeType.Modified,
                _ => FileChangeType.Deleted
            };
            FileChange? change = job.Changes?.FirstOrDefault(item => (item.ChangeType == kind || kind == FileChangeType.Modified && item.ChangeType == FileChangeType.Unchanged)
                && (item.Current ?? item.Previous)?.RelativePath == relativePath);

            if (change is null)
            {
                return new ConflictObjectResult("Файл не относится к выбранному действию сравнения.");
            }
            try
            {
                token.ThrowIfCancellationRequested();
                if (operation == FileMutationOperation.Upload)
                {
                    await _files.CopyMissingAsync(job.SourceRootPath, change.Previous!, body, token);
                }
                else if (operation is FileMutationOperation.Delete or FileMutationOperation.DeleteCompared)
                {
                    _files.DeleteUnchanged(job.SourceRootPath, change.Current!);
                }
                else if (operation == FileMutationOperation.VerifyUnchanged)
                {
                    using Stream source = _reader.OpenRead(job.SourceRootPath, change.Current!);
                }
                else
                {
                    _files.EnsureMissing(job.SourceRootPath, relativePath);
                }
                return new NoContentResult();
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
            catch (ArgumentException)
            {
                return new BadRequestResult();
            }
        }
    }
}
