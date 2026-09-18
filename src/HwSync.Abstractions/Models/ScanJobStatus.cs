namespace HwSync.Abstractions.Models
{
    /// <summary>
    /// Состояния задания в серверной очереди.
    /// </summary>
    public enum ScanJobStatus
    {
        Queued,
        Running,
        CancellationRequested,
        Completed,
        Cancelled,
        Failed
    }
}
