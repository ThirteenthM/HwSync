using System.Collections.ObjectModel;
using System.Diagnostics;

namespace HwSync.Client.Windows.Diagnostics
{
    /// <summary>
    /// Замеры одной операции копирования с учётом накладных расходов.
    /// </summary>
    public sealed class TransferMetrics
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly ObservableCollection<FileTransferMeasurement> _files = new();
        private long _confirmedBytes;
        private int _completedFiles;

        /// <summary>
        /// Начинает измерение выбранного направления передачи.
        /// </summary>
        public TransferMetrics(string direction)
        {
            Direction = direction;
            Files = new(_files);
        }

        public string Direction
        {
            get;
        }
        public ReadOnlyObservableCollection<FileTransferMeasurement> Files
        {
            get;
        }
        public long ConfirmedBytes => _confirmedBytes;
        public TimeSpan Duration => _clock.Elapsed;
        public double MebibytesPerSecond => Duration.TotalSeconds > 0
            ? _confirmedBytes / 1048576d / Duration.TotalSeconds : 0;

        /// <summary>
        /// Добавляет попытку, учитывая объём только успешно завершённых файлов.
        /// </summary>
        public void Record(string path, long size, TimeSpan duration, bool completed, string outcome)
        {
            long bytes = completed ? size : 0;
            _files.Add(new(path, bytes, duration, outcome));
            _confirmedBytes += bytes;
            if (completed)
            {
                _completedFiles++;
            }
        }

        /// <summary>
        /// Останавливает общий таймер после завершения, ошибки или отмены.
        /// </summary>
        public void Complete()
        {
            _clock.Stop();
        }

        /// <summary>
        /// Формирует сводку по подтверждённым файлам и полному времени операции.
        /// </summary>
        public string Describe() =>
            $"{Direction}: файлов {_completedFiles}, {_confirmedBytes / 1048576d:F2} МиБ; " +
            $"{Duration.TotalSeconds:F2} с; средняя скорость {MebibytesPerSecond:F2} МиБ/с.";
    }
}
