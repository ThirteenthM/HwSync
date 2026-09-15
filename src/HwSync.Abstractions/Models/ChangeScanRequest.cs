namespace HwSync.Abstractions.Models
{
    public sealed record ChangeScanRequest(
        string RootPath,
        IReadOnlyCollection<FileSnapshot> PreviousSnapshot
    );
}
