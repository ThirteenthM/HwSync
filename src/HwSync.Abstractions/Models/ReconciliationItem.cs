namespace HwSync.Abstractions.Models
{
    /// <summary>Решение по файлу с исходными состояниями сторон.</summary>
    public sealed record ReconciliationItem(string RelativePath, ReconciliationAction Action, FileVersion? Baseline, FileVersion? Server, FileVersion? Client);
}
