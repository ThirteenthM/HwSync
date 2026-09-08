using HwSync.Abstractions.Models;

namespace HwSync.Core.Models
{
    public sealed record ChangeScanRequest(
        string RootPath,
        IReadOnlyCollection<FileSnapshot> PreviousSnapshot
    );
}
