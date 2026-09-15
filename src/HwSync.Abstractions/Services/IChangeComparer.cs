using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.Services
{
    public interface IChangeComparer
    {
        IReadOnlyCollection<FileChange> Compare(
            IReadOnlyCollection<FileSnapshot> previous,
            IReadOnlyCollection<FileSnapshot> current);
    }
}
