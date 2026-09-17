using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using HwSync.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;
namespace HwSync.Api.Handlers
{
    public sealed class FileMutationHandler
    {
        private readonly IScanJobService _jobs;
        private readonly IComparedFileOperations _files;
        public FileMutationHandler(IScanJobService jobs, IComparedFileOperations files)
        { _jobs = jobs; _files = files; }

        public async Task<IActionResult> ExecuteAsync(Guid id, string relativePath, string operation, Stream body, CancellationToken token)
        {
            ScanJob? job = _jobs.Get(id);
            if (job is null) { return new NotFoundResult(); }
            if (job.Status != ScanJobStatus.Completed || job.SourceRootPath is null) { return new ConflictResult(); }

            FileChangeType kind = operation == "delete" ? FileChangeType.Created : FileChangeType.Deleted;
            FileChange? change = job.Changes?.FirstOrDefault(item => item.ChangeType == kind
                && (item.Current ?? item.Previous)?.RelativePath == relativePath);

            if (change is null) { return new ConflictObjectResult("Файл не относится к выбранному действию сравнения."); }
            try
            {
                token.ThrowIfCancellationRequested();
                if (operation == "upload")
                { await _files.CopyMissingAsync(job.SourceRootPath, change.Previous!, body, token); }
                else if (operation == "delete")
                { _files.DeleteUnchanged(job.SourceRootPath, change.Current!); }
                else { _files.EnsureMissing(job.SourceRootPath, relativePath); }
                return new NoContentResult();
            }
            catch (IOException exception) { return new ConflictObjectResult(new ProblemDetails { Status = 409, Detail = exception.Message }); }
            catch (UnauthorizedAccessException) { return new StatusCodeResult(403); }
            catch (ArgumentException) { return new BadRequestResult(); }
        }
    }
}
