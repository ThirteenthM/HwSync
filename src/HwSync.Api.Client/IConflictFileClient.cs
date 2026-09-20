namespace HwSync.Api.Client
{
    /// <summary>
    /// Передача клиентской версии для разрешения конфликта.
    /// </summary>
    public interface IConflictFileClient
    {
        /// <summary>
        /// Заменяет неизменившуюся серверную версию клиентской.
        /// </summary>
        Task ReplaceServerFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token);

        /// <summary>
        /// Сохраняет клиентскую версию на сервере под отдельным именем.
        /// </summary>
        Task PreserveClientFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token);
    }
}
