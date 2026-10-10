using System.Threading.Channels;
using HwSync.Abstractions.FileSystem;
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
        private readonly Dictionary<Guid, CancellationTokenSource> _scanCancellations = [];
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
            if (request.PreviousSnapshot is null)
            {
                throw new ArgumentException("PreviousSnapshot должен содержать файлы с непустыми уникальными путями.");
            }

            HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
            foreach (FileSnapshot file in request.PreviousSnapshot)
            {
                if (file is null || string.IsNullOrWhiteSpace(file.RelativePath)
                    || !paths.Add(RelativeFilePath.Key(file.RelativePath)))
                {
                    throw new ArgumentException("PreviousSnapshot должен содержать файлы с непустыми уникальными путями.");
                }
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
        /// Запрашивает отмену задания и прерывает его чтение.
        /// </summary>
        public ScanJob? Cancel(Guid id)
        {
            CancellationTokenSource? scan = null;
            ScanJob result;
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
                    _scanCancellations.TryGetValue(id, out scan);
                }

                _jobs[id] = job;
                result = job;
            }

            CancelScan(scan);
            return result;
        }

        /// <summary>
        /// Последовательно обрабатывает очередь до остановки хоста.
        /// Каждое чтение получает токен, связанный с остановкой хоста и отменой этого задания.
        /// </summary>
        public async Task RunAsync(Func<ChangeScanRequest, CancellationToken, IReadOnlyCollection<FileChange>> scan, CancellationToken stoppingToken)
        {
            using CancellationTokenRegistration registration = stoppingToken.Register(Stop);
            try
            {
                await foreach ((Guid id, ChangeScanRequest request) in _queue.Reader.ReadAllAsync(stoppingToken))
                {
                    CancellationTokenSource? scanCancellation = TryBeginScan(id, out bool stopping);
                    if (scanCancellation is null)
                    {
                        if (stopping)
                        {
                            break;
                        }

                        continue;
                    }

                    try
                    {
                        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                            stoppingToken, scanCancellation.Token);
                        IReadOnlyCollection<FileChange>? changes = null;
                        string? error = null;
                        try
                        {
                            changes = scan(request, linked.Token);
                        }
                        catch (OperationCanceledException) when (linked.IsCancellationRequested)
                        {
                            // Статус Cancelled выставляется ниже и для остановки хоста,
                            // и для отмены этого задания.
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
                                || linked.IsCancellationRequested;
                            _jobs[id] = job with
                            {
                                Status = cancelled ? ScanJobStatus.Cancelled : error is null ? ScanJobStatus.Completed : ScanJobStatus.Failed,
                                FinishedAt = DateTimeOffset.UtcNow,
                                Changes = cancelled ? null : changes,
                                Error = cancelled ? null : error
                            };
                        }
                    }
                    finally
                    {
                        lock (_gate)
                        {
                            _scanCancellations.Remove(id);
                        }

                        scanCancellation.Dispose();
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
        /// Переводит ожидающее задание в выполнение и выдаёт источник его отмены.
        /// </summary>
        private CancellationTokenSource? TryBeginScan(Guid id, out bool stopping)
        {
            lock (_gate)
            {
                stopping = _stopping;
                if (_stopping || !_jobs.TryGetValue(id, out ScanJob? job) || job.Status != ScanJobStatus.Queued)
                {
                    return null;
                }

                CancellationTokenSource scanCancellation = new();
                _scanCancellations.Add(id, scanCancellation);
                _jobs[id] = job with
                {
                    Status = ScanJobStatus.Running
                };
                return scanCancellation;
            }
        }

        /// <summary>
        /// Прерывает чтение задания; повтор после освобождения источника безопасен.
        /// </summary>
        private static void CancelScan(CancellationTokenSource? scan)
        {
            if (scan is null)
            {
                return;
            }

            try
            {
                scan.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Исполнитель уже завершил задание и освободил источник.
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
