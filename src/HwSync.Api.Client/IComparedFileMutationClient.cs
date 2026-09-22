namespace HwSync.Api.Client
{
    /// <summary>
    /// Ручное удаление одной версии файла, присутствовавшего на обеих сторонах.
    /// </summary>
    public interface IComparedFileMutationClient
    {
        /// <summary>
        /// Проверяет серверную версию перед удалением клиентской.
        /// </summary>
        Task EnsureServerFileUnchangedAsync(Guid jobId, string relativePath, CancellationToken token);

        /// <summary>
        /// Удаляет неизменённую серверную версию двустороннего различия.
        /// </summary>
        Task DeleteComparedServerFileAsync(Guid jobId, string relativePath, CancellationToken token);
    }
}
