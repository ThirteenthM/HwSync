using HwSync.Abstractions.Models;
using HwSync.Core.Services;

namespace HwSync.Core.Tests.Services
{
    /// <summary>Проверки обработки и отмены очереди заданий.</summary>
    public class ScanJobServiceTests
    {
        /// <summary>Проверяет отмену ожидающих и выполняющихся заданий.</summary>
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
            Task worker = Task.Run(() => jobs.RunAsync(scanRequest =>
            {
                entered.SetResult();
                release.Wait(stop.Token);
                return Array.Empty<FileChange>();
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

        /// <summary>Проверяет продолжение очереди после ошибки сканирования.</summary>
        [Test]
        public async Task FailedScan_DoesNotStopNextJob()
        {
            ScanJobService jobs = new();
            ChangeScanRequest request = new(Path.GetTempPath(), []);
            ScanJob first = jobs.Start(request);
            ScanJob second = jobs.Start(request);
            int calls = 0;
            using CancellationTokenSource stop = new(TimeSpan.FromSeconds(10));
            Task worker = Task.Run(() => jobs.RunAsync(scanRequest =>
            {
                if (++calls == 1)
                {
                    throw new IOException("private details");
                }
                return Array.Empty<FileChange>();
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
