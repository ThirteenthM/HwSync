namespace HwSync.Api.Client
{
    public interface IFileDownloadClient
    {
        Task DownloadFileAsync(Guid jobId, string relativePath, Stream destination, CancellationToken cancellationToken = default);
    }
}
