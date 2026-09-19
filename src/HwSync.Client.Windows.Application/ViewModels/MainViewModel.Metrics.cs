using HwSync.Client.Windows.Contract.ViewModels;
using System.Diagnostics;
using HwSync.Abstractions.Models;
using HwSync.Client.Windows.Contract.Diagnostics;
using HwSync.Client.Windows.Application.Diagnostics;

namespace HwSync.Client.Windows.Application.ViewModels
{
    /// <summary>
    /// Необязательные замеры операций копирования для формы клиента.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private TransferMetrics? _transferMetrics;
        private string _metricsSummary = "";

        public bool TransferMetricsEnabled { get; init; } = true;

        public TransferMetrics? TransferMetrics
        {
            get => _transferMetrics;
            private set
            {
                SetProperty(ref _transferMetrics, value);
                OnPropertyChanged(nameof(HasTransferMetrics));
            }
        }

        public string MetricsSummary
        {
            get => _metricsSummary;
            private set => SetProperty(ref _metricsSummary, value);
        }

        public bool HasTransferMetrics => TransferMetrics is not null;

        /// <summary>
        /// Создаёт замер только при включённой настройке.
        /// </summary>
        private void BeginTransferMetrics(string direction, bool isCopy = true)
        {
            TransferMetrics = TransferMetricsEnabled && isCopy ? new(direction) : null;
            MetricsSummary = TransferMetrics?.Describe() ?? "";
        }

        /// <summary>
        /// Измеряет попытку копирования, сохраняя ошибку и отмену для вызывающего кода.
        /// </summary>
        private async Task<T> MeasureTransferAsync<T>(FileSnapshot file, Func<Task<T>> transfer, Func<T, bool> isCompleted)
        {
            TransferMetrics? metrics = TransferMetrics;
            if (metrics is null)
            {
                return await transfer();
            }

            long started = Stopwatch.GetTimestamp();
            bool completed = false;
            string outcome = "Ошибка";
            try
            {
                T result = await transfer();
                completed = isCompleted(result);
                outcome = completed ? "Скопирован" : "Пропущен / ошибка";
                return result;
            }
            catch (OperationCanceledException)
            {
                outcome = "Прерван";
                throw;
            }
            finally
            {
                metrics.Record(file.RelativePath, file.Size, Stopwatch.GetElapsedTime(started), completed, outcome);
                MetricsSummary = metrics.Describe();
            }
        }

        /// <summary>
        /// Фиксирует итоговую длительность операции и обновляет сводку.
        /// </summary>
        private void CompleteTransferMetrics()
        {
            TransferMetrics?.Complete();
            MetricsSummary = TransferMetrics?.Describe() ?? "";
        }
    }
}
