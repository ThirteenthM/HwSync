using HwSync.Abstractions.Models;
using HwSync.Core.Services;

namespace HwSync.Core.Tests.Services
{
    /// <summary>
    /// Проверки обработки и отмены очереди заданий.
    /// </summary>
    public class ScanJobServiceTests
    {
        /// <summary>
        /// Остановка хоста прерывает текущее сканирование через переданный токен.
        /// </summary>
        [Test]
        public async Task Stop_CancelsActiveScan()
        {
            ScanJobService jobs = new();
            ScanJob job = jobs.Start(new(Path.GetTempPath(), []));
            TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenSource stop = new();
            Task worker = Task.Run(() => jobs.RunAsync((request, token) =>
            {
                entered.TrySetResult();
                token.WaitHandle.WaitOne();
                token.ThrowIfCancellationRequested();
                return [];
            }, stop.Token));
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await stop.CancelAsync();
                await worker.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.That(jobs.Get(job.Id)!.Status, Is.EqualTo(ScanJobStatus.Cancelled));
                Assert.That(jobs.Get(job.Id)!.Changes, Is.Null);
                Assert.That(jobs.Get(job.Id)!.Error, Is.Null);
            }
            finally
            {
                await stop.CancelAsync();
                await worker.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        /// <summary>
        /// OperationCanceledException по токену хоста не превращается в Failed.
        /// </summary>
        [Test]
        public async Task Stop_OperationCanceledException_IsCancelledNotFailed()
        {
            ScanJobService jobs = new();
            ScanJob job = jobs.Start(new(Path.GetTempPath(), []));
            TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenSource stop = new();
            Task worker = Task.Run(() => jobs.RunAsync((request, token) =>
            {
                entered.TrySetResult();
                SpinWait.SpinUntil(() => token.IsCancellationRequested, TimeSpan.FromSeconds(5));
                throw new OperationCanceledException(token);
            }, stop.Token));
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await stop.CancelAsync();
                await worker.WaitAsync(TimeSpan.FromSeconds(5));
                ScanJob? finished = jobs.Get(job.Id);
                Assert.That(finished!.Status, Is.EqualTo(ScanJobStatus.Cancelled));
                Assert.That(finished.Error, Is.Null);
                Assert.That(finished.Changes, Is.Null);
            }
            finally
            {
                await stop.CancelAsync();
                await worker.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        /// <summary>
        /// Отмена не позволяет начать обход файловой системы.
        /// </summary>
        [Test]
        public void Snapshot_CancelledBeforeStart_DoesNotAccessFolder()
        {
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            HwSync.Infrastructure.FileSystem.DirectorySnapshotProvider provider = new();
            Assert.Throws<OperationCanceledException>(() =>
                provider.GetSnapshot(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()), cancellation.Token));
        }
        /// <summary>
        /// Проверяет отмену ожидающих и выполняющихся заданий.
        /// </summary>
        [Test]
        public async Task Cancel_QueuedAndRunningJobs_DiscardsResults()
        {
            ScanJobService jobs = new();
            ChangeScanRequest request = new(Path.GetTempPath(), []);
            ScanJob queued = jobs.Start(request);
            Assert.That(jobs.Cancel(queued.Id)!.Status, Is.EqualTo(ScanJobStatus.Cancelled));
            ScanJob running = jobs.Start(request);
            TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using ManualResetEventSlim release = new();
            using CancellationTokenSource stop = new(TimeSpan.FromSeconds(10));
            Task worker = Task.Run(() => jobs.RunAsync((scanRequest, cancellationToken) =>
            {
                entered.SetResult();
                release.Wait(stop.Token);
                return [];
            }, stop.Token));
            try
            {
                await entered.Task.WaitAsync(stop.Token);
                Assert.That(jobs.Cancel(running.Id)!.Status, Is.EqualTo(ScanJobStatus.CancellationRequested));
                release.Set();
                while (jobs.Get(running.Id)!.FinishedAt is null)
                {
                    await Task.Delay(10, stop.Token);
                }
                Assert.Multiple(() =>
                {
                    Assert.That(jobs.Get(running.Id)!.Status, Is.EqualTo(ScanJobStatus.Cancelled));
                    Assert.That(jobs.Get(running.Id)!.Changes, Is.Null);
                });
            }
            finally
            {
                release.Set();
                await stop.CancelAsync();
                await worker;
            }
            Assert.Throws<InvalidOperationException>(() => jobs.Start(request));
        }

        /// <summary>
        /// Проверяет продолжение очереди после ошибки сканирования.
        /// </summary>
        [Test]
        public async Task FailedScan_DoesNotStopNextJob()
        {
            ScanJobService jobs = new();
            ChangeScanRequest request = new(Path.GetTempPath(), []);
            ScanJob first = jobs.Start(request);
            ScanJob second = jobs.Start(request);
            int calls = 0;
            using CancellationTokenSource stop = new(TimeSpan.FromSeconds(10));
            Task worker = Task.Run(() => jobs.RunAsync((scanRequest, cancellationToken) =>
            {
                if (++calls == 1)
                {
                    throw new IOException("private details");
                }
                return [];
            }, stop.Token));
            try
            {
                while (jobs.Get(second.Id)!.FinishedAt is null)
                {
                    await Task.Delay(10, stop.Token);
                }
                Assert.Multiple(() =>
                {
                    Assert.That(jobs.Get(first.Id)!.Status, Is.EqualTo(ScanJobStatus.Failed));
                    Assert.That(jobs.Get(first.Id)!.Error, Does.Not.Contain("private details"));
                    Assert.That(jobs.Get(second.Id)!.Status, Is.EqualTo(ScanJobStatus.Completed));
                });
            }
            finally
            {
                await stop.CancelAsync();
                await worker;
            }
        }
    }
}
