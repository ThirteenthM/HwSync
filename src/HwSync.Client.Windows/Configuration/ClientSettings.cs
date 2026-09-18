namespace HwSync.Client.Windows.Configuration
{
    /// <summary>
    /// Адреса, пути и тайм-аут передачи файлов клиента.
    /// </summary>
    public sealed class ClientSettings
    {
        public bool TransferMetricsEnabled { get; init; } = true;

        public int FileTransferTimeoutSeconds { get; init; } = 1800;

        public string ServerAddress { get; init; } = "http://localhost:5080";

        public string ServerRootPath { get; init; } = string.Empty;

        public string ClientRootPath { get; init; } = string.Empty;
    }
}
