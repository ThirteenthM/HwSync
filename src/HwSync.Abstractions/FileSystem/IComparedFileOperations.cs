using HwSync.Abstractions.Models;
namespace HwSync.Abstractions.FileSystem
{
    public interface IComparedFileOperations
    {
        Task CopyMissingAsync(string root, FileSnapshot file, Stream content, CancellationToken token);
        void DeleteUnchanged(string root, FileSnapshot file);
        void EnsureMissing(string root, string relativePath);
    }
}
