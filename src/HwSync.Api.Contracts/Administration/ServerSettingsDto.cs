namespace HwSync.Api.Contracts.Administration
{
    /// <summary>
    /// Разрешённые для просмотра настройки сервера без секретов.
    /// </summary>
    public sealed record ServerSettingsDto(
        string Version, string MachineName, string DatabasePath, string[] ListeningAddresses,
        string DefaultLogLevel, bool LocalConnectionsOnly, string UserName);
}
