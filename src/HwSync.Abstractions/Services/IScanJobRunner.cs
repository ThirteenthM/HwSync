using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.Services
{
    /// <summary>
    /// Последовательная обработка очереди заданий сканирования.
    /// </summary>
    public interface IScanJobRunner
    {
        /// <summary>
        /// Обрабатывает задания до остановки, передавая отмену сканированию.
        /// </summary>
        Task RunAsync(Func<ChangeScanRequest, CancellationToken, IReadOnlyCollection<FileChange>> scan, CancellationToken stoppingToken);
    }
}