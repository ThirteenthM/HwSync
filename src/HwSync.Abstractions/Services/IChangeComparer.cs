using HwSync.Abstractions.Models;

namespace HwSync.Abstractions.Services
{
    /// <summary>Сравнение двух снимков файлов.</summary>
    public interface IChangeComparer
    {
        /// <summary>Возвращает различия снимков по пути, размеру и времени изменения.</summary>
        IReadOnlyCollection<FileChange> Compare(
            IReadOnlyCollection<FileSnapshot> previous,
            IReadOnlyCollection<FileSnapshot> current);
    }
}
