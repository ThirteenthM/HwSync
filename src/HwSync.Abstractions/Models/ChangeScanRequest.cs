namespace HwSync.Abstractions.Models
{
    /// <summary>Корневая папка и предыдущий снимок для сканирования.</summary>
    public sealed record ChangeScanRequest(
        string RootPath,
        IReadOnlyCollection<FileSnapshot> PreviousSnapshot
    );
}
