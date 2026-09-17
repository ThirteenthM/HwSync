using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.Services
{
    /// <summary>Сканирование папки и сравнение с предыдущим снимком.</summary>
    public interface IChangeScanner
    {
        /// <summary>Читает папку, сравнивает снимки и обновляет историю.</summary>
        IReadOnlyCollection<FileChange> Scan(ChangeScanRequest request);
    }
}
