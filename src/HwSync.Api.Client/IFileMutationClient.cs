namespace HwSync.Api.Client
{
    public interface IFileMutationClient
    {
        Task UploadFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token);
        Task DeleteServerFileAsync(Guid jobId, string relativePath, CancellationToken token);
        Task EnsureServerFileMissingAsync(Guid jobId, string relativePath, CancellationToken token);
    }
}
