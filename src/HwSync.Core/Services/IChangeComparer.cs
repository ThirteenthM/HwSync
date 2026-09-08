using HwSync.Abstractions.Models;
using HwSync.Core.Models;

namespace HwSync.Core.Services
{
    public interface IChangeComparer
    {
        IReadOnlyCollection<FileChange> Compare(
            IReadOnlyCollection<FileSnapshot> previous,
            IReadOnlyCollection<FileSnapshot> current);
    }
}
