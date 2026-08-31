using HwSync.Abstractions.Models;

namespace HwSync.Core.Models
{
    public sealed record FileChange(
        FileChangeType ChangeType,
        FileSnapshot? Previous,
        FileSnapshot? Current
    );
}
