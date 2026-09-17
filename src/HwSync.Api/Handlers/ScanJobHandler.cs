using HwSync.Abstractions.Models;
using HwSync.Abstractions.Services;
using HwSync.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HwSync.Api.Handlers
{
    /// <summary>Преобразование запросов API в операции над заданиями.</summary>
    public sealed class ScanJobHandler
    {
        private readonly IScanJobService _jobs;

        /// <summary>Принимает службу серверных заданий.</summary>
        public ScanJobHandler(IScanJobService jobs)
        {
            _jobs = jobs;
        }

        /// <summary>Ставит сравнение папки в очередь.</summary>
        public ActionResult<ScanJobResponse> Start(CompareFoldersRequest request)
        {
            try
            {
                if (request.ClientSnapshot is null || request.ClientSnapshot.Any(file => file is null))
                {
                    throw new ArgumentException("ClientSnapshot должен быть массивом снимков файлов.");
                }
                ChangeScanRequest scanRequest = new(request.ServerRootPath,
                    request.ClientSnapshot.Select(file => new FileSnapshot(file.RelativePath, file.Size, file.LastWriteTimeUtc)).ToArray());
                ScanJob job = _jobs.Start(scanRequest);
                return new AcceptedResult($"/api/v1/scan-jobs/{job.Id}", ToResponse(job));
            }
            catch (ArgumentException exception)
            {
                return new BadRequestObjectResult(new ProblemDetails
                {
                    Status = 400,
                    Detail = exception.Message
                });
            }
            catch (InvalidOperationException exception)
            {
                return new ConflictObjectResult(new ProblemDetails
                {
                    Status = 409,
                    Detail = exception.Message
                });
            }
        }

        /// <summary>Возвращает состояние задания по идентификатору.</summary>
        public ActionResult<ScanJobResponse> Get(Guid id)
        {
            return _jobs.Get(id) is ScanJob job ? ToResponse(job) : new NotFoundResult();
        }

        /// <summary>Запрашивает отмену задания по идентификатору.</summary>
        public ActionResult<ScanJobResponse> Cancel(Guid id)
        {
            return _jobs.Cancel(id) is ScanJob job ? ToResponse(job) : new NotFoundResult();
        }

        /// <summary>Преобразует внутреннее задание в контракт API.</summary>
        private static ScanJobResponse ToResponse(ScanJob job)
        {
            return new(job.Id, job.Status switch
            {
                ScanJobStatus.Queued => ScanJobState.Queued,
                ScanJobStatus.Running => ScanJobState.Running,
                ScanJobStatus.CancellationRequested => ScanJobState.CancellationRequested,
                ScanJobStatus.Completed => ScanJobState.Completed,
                ScanJobStatus.Cancelled => ScanJobState.Cancelled,
                ScanJobStatus.Failed => ScanJobState.Failed,
                _ => throw new ArgumentOutOfRangeException(nameof(job))
            }, job.CreatedAt, job.FinishedAt, job.Changes?.Select(change => new FileChangeDto(
                change.ChangeType switch
                {
                    FileChangeType.Created => FileChangeKind.Created,
                    FileChangeType.Modified => FileChangeKind.Modified,
                    FileChangeType.Deleted => FileChangeKind.Deleted,
                    _ => throw new ArgumentOutOfRangeException(nameof(job))
                }, ToDto(change.Previous), ToDto(change.Current))).ToArray(), job.Error);
        }

        /// <summary>Преобразует снимок файла в контракт API.</summary>
        private static FileSnapshotDto? ToDto(FileSnapshot? file)
        {
            return file is null ? null : new(file.RelativePath, file.Size, file.LastWriteTimeUtc);
        }
    }
}
