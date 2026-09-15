namespace HwSync.Abstractions.Models
{
    public sealed record FileChange(
        FileChangeType ChangeType,
        FileSnapshot? Previous,
        FileSnapshot? Current
    );
}
