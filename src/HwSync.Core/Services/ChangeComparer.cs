using HwSync.Abstractions.Services;
using HwSync.Abstractions.Models;

namespace HwSync.Core.Services
{
    /// <summary>
    /// Сравнивает снимки по пути, размеру и времени изменения.
    /// </summary>
    public sealed class ChangeComparer : IChangeComparer
    {
        /// <summary>
        /// Возвращает только новые, изменённые и исчезнувшие файлы.
        /// </summary>
        public IReadOnlyCollection<FileChange> Compare(
            IReadOnlyCollection<FileSnapshot> previous,
            IReadOnlyCollection<FileSnapshot> current
        )
        {
            Dictionary<string, FileSnapshot> previousByPath = previous.ToDictionary(x => x.RelativePath);
            Dictionary<string, FileSnapshot> currentByPath = current.ToDictionary(x => x.RelativePath);

            List<FileChange> changes = [];

            foreach (FileSnapshot currentFile in currentByPath.Values)
            {
                if (!previousByPath.TryGetValue(currentFile.RelativePath, out FileSnapshot? previousFile))
                {
                    // у текущего файла нет предыдущего - новый файл.
                    changes.Add(new(FileChangeType.Created, Previous: null, Current: currentFile));
                    continue;
                }

                if (IsModified(previousFile, currentFile))
                {
                    // у текущего файла есть предыдущий и они различаются.
                    changes.Add(new(FileChangeType.Modified, Previous: previousFile, Current: currentFile));
                }
            }

            foreach (FileSnapshot previousFile in previousByPath.Values)
            {
                if (!currentByPath.ContainsKey(previousFile.RelativePath))
                {
                    // был файл предыдущий и щас его нет среди текущих - файл был удален.
                    changes.Add(new(FileChangeType.Deleted, Previous: previousFile, Current: null));
                }
            }

            return changes;
        }

        /// <summary>
        /// Проверяет различие размера или времени записи.
        /// </summary>
        private static bool IsModified(
            FileSnapshot previousFile,
            FileSnapshot currentFile
        )
        {
            // !!!! пока признак различия - это разный размер и разная дата последней модификации.
            return
                previousFile.Size != currentFile.Size
                ||
                previousFile.LastWriteTimeUtc != currentFile.LastWriteTimeUtc;
        }
    }
}
