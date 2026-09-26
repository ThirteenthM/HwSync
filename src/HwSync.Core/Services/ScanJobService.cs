using System.Threading.Channels;
using HwSync.Abstractions.Models;
using HwSync.Abstractions.Services;

namespace HwSync.Core.Services
{
    /// <summary>
    /// Ограниченная очередь заданий с хранением результатов в памяти.
    /// </summary>
    public sealed class ScanJobService : IScanJobService, IScanJobRunner
    {
        private const int Capacity = 100;
        private readonly System.Threading.Lock _gate = new();
        private readonly Dictionary<Guid, ScanJob> _jobs = [];
        private readonly Channel<(Guid Id, ChangeScanRequest Request)> _queue =
            Channel.CreateBounded<(Guid, ChangeScanRequest)>(Capacity);
        private bool _stopping;

        /// <summary>
        /// Ставит сравнение папки в очередь.
        /// </summary>
        public ScanJob Start(ChangeScanRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.RootPath) || !Path.IsPathFullyQualified(request.RootPath))
            {
                throw new ArgumentException("RootPath должен быть абсолютным путём.");
            }
            if (request.PreviousSnapshot is null || request.PreviousSnapshot.Any(file =>
                file is null || string.IsNullOrWhiteSpace(file.RelativePath))
                || request.PreviousSnapshot.Select(file => file.RelativePath).Distinct().Count() != request.PreviousSnapshot.Count)
            {
                throw new ArgumentException("PreviousSnapshot должен содержать файлы с непустыми уникальными путями.");
            }
            ChangeScanRequest snapshot = new(request.RootPath, request.PreviousSnapshot.ToArray());
            lock (_gate)
            {
                if (_stopping)
                {
                    throw new InvalidOperationException("Host останавливается.");
                }
                if (_jobs.Count >= Capacity)
                {
                    ScanJob? oldest = _jobs.Values.Where(job => job.FinishedAt is not null)
                        .OrderBy(job => job.FinishedAt).FirstOrDefault() ?? throw new InvalidOperationException("Очередь заданий заполнена.");
                    _jobs.Remove(oldest.Id);
                }
                ScanJob job = new(Guid.NewGuid(), ScanJobStatus.Queued, DateTimeOffset.UtcNow, null, null, null, Path.GetFullPath(request.RootPath));
                if (!_queue.Writer.TryWrite((job.Id, snapshot)))
                {
                    throw new InvalidOperationException("Очередь заданий заполнена.");
                }
                _jobs.Add(job.Id, job);
                return job;
            }
        }

        /// <summary>
        /// Возвращает состояние задания по идентификатору.
        /// </summary>
        public ScanJob? Get(Guid id)
        {
            lock (_gate)
            {
                return _jobs.GetValueOrDefault(id);
            }
        }

        /// <summary>
        /// Запрашивает отмену задания по идентификатору.
        /// </summary>
        public ScanJob? Cancel(Guid id)
        {
            lock (_gate)
            {
                if (!_jobs.TryGetValue(id, out ScanJob? job))
                {
                    return null;
                }
                if (job.Status == ScanJobStatus.Queued)
                {
                    job = job with
                    {
                        Status = ScanJobStatus.Cancelled,
                        FinishedAt = DateTimeOffset.UtcNow
                    };
                }
                else if (job.Status == ScanJobStatus.Running)
                {
                    job = job with
                    {
                        Status = ScanJobStatus.CancellationRequested
                    };
                }
                _jobs[id] = job;
                return job;
            }
        }

        /// <summary>
        /// Последовательно обрабатывает очередь до отмены работы.
        /// </summary>
        public async Task RunAsync(Func<ChangeScanRequest, CancellationToken, IReadOnlyCollection<FileChange>> scan, CancellationToken stoppingToken)
        {
            using CancellationTokenRegistration registration = stoppingToken.Register(Stop);
            try
            {
                await foreach ((Guid id, ChangeScanRequest request) in _queue.Reader.ReadAllAsync(stoppingToken))
                {
                    lock (_gate)
                    {
                        if (_stopping)
                        {
                            break;
                        }
                        if (!_jobs.TryGetValue(id, out ScanJob? job) || job.Status != ScanJobStatus.Queued)
                        {
                            continue;
                        }
                        _jobs[id] = job with
                        {
                            Status = ScanJobStatus.Running
                        };
                    }
                    IReadOnlyCollection<FileChange>? changes = null;
                    string? error = null;
                    try
                    {
                        changes = scan(request, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // Статус Cancelled выставляется ниже по токену остановки,
                        // даже если callback Register(Stop) ещё не успел выставить _stopping.
                    }
                    catch (Exception exception)
                    {
                        error = exception is IOException or UnauthorizedAccessException
                            ? "Не удалось прочитать каталог. Проверьте путь и права доступа."
                            : "Не удалось выполнить сравнение снимков.";
                    }
                    lock (_gate)
                    {
                        ScanJob job = _jobs[id];
                        bool cancelled = _stopping
                            || job.Status == ScanJobStatus.CancellationRequested
                            || stoppingToken.IsCancellationRequested;
                        _jobs[id] = job with
                        {
                            Status = cancelled ? ScanJobStatus.Cancelled : error is null ? ScanJobStatus.Completed : ScanJobStatus.Failed,
                            FinishedAt = DateTimeOffset.UtcNow,
                            Changes = cancelled ? null : changes,
                            Error = cancelled ? null : error
                        };
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {

            }
            finally
            {
                Stop();
            }
        }

        /// <summary>
        /// Отмечает оставшиеся задания отменёнными при остановке службы.
        /// </summary>
        private void Stop()
        {
            lock (_gate)
            {
                _stopping = true;
                _queue.Writer.TryComplete();
                foreach (ScanJob job in _jobs.Values.ToArray())
                {
                    if (job.Status == ScanJobStatus.Queued)
                    {
                        _jobs[job.Id] = job with
                        {
                            Status = ScanJobStatus.Cancelled,
                            FinishedAt = DateTimeOffset.UtcNow
                        };
                    }
                    else if (job.Status == ScanJobStatus.Running)
                    {
                        _jobs[job.Id] = job with
                        {
                            Status = ScanJobStatus.CancellationRequested
                        };
                    }
                }
            }
        }
    }
}
