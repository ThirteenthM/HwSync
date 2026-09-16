using HwSync.Abstractions.Models;
namespace HwSync.Abstractions.FileSystem
{
    public interface ISourceFileReader
    {
        Stream OpenRead(string rootPath, FileSnapshot expected);
    }
}
