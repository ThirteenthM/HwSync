namespace HwSync.Api.Client
{
    /// <summary>
    /// Загрузка файлов с сервера по результату сравнения.
    /// </summary>
    public interface IFileDownloadClient
    {
        /// <summary>
        /// Скачивает файл из результата сравнения в переданный поток.
        /// </summary>
        Task DownloadFileAsync(Guid jobId, string relativePath, Stream destination, CancellationToken cancellationToken = default);
    }
}
