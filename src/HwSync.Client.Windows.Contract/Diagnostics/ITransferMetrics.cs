using System.Collections.ObjectModel;

namespace HwSync.Client.Windows.Contract.Diagnostics
{
    /// <summary>
    /// Доступные форме результаты измерения копирования.
    /// </summary>
    public interface ITransferMetrics
    {
        string Direction { get; }

        ReadOnlyObservableCollection<FileTransferMeasurement> Files { get; }

        long ConfirmedBytes { get; }

        TimeSpan Duration { get; }

        double MegabytesPerSecond { get; }
    }
}
