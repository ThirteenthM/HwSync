namespace HwSync.Client.Windows.Configuration
{
    public sealed class ClientSettings
    {
        public string ServerAddress { get; init; } = "http://localhost:5080";
        public string ServerRootPath { get; init; } = string.Empty;
        public string ClientRootPath { get; init; } = string.Empty;
    }
}
