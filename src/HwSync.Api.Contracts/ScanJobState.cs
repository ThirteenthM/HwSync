namespace HwSync.Api.Contracts
{
    /// <summary>
    /// Состояния задания, доступные клиенту.
    /// </summary>
    public enum ScanJobState
    {
        Queued, Running, CancellationRequested, Completed, Cancelled, Failed
    }
}
