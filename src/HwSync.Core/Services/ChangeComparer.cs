using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Services;
using HwSync.Abstractions.Models;

namespace HwSync.Core.Services
{
    /// <summary>
    /// Сравнивает снимки по Windows-пути без учёта регистра, размеру и времени изменения.
    /// Один файл с разным регистром или разделителем остаётся одним путём.
    /// </summary>
    public sealed class ChangeComparer : IChangeComparer
    {
        /// <summary>
        /// Возвращает результат сравнения для всех файлов, включая одинаковые.
        /// </summary>
        public IReadOnlyCollection<FileChange> Compare(
            IReadOnlyCollection<FileSnapshot> previous,
            IReadOnlyCollection<FileSnapshot> current
        )
        {
            Dictionary<string, FileSnapshot> previousByPath = Index(previous);
            Dictionary<string, FileSnapshot> currentByPath = Index(current);

            List<FileChange> changes = [];

            foreach (FileSnapshot currentFile in currentByPath.Values)
            {
                if (!previousByPath.TryGetValue(RelativeFilePath.Key(currentFile.RelativePath), out FileSnapshot? previousFile))
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
                else
                {
                    changes.Add(new(FileChangeType.Unchanged, Previous: previousFile, Current: currentFile));
                }
            }

            foreach (FileSnapshot previousFile in previousByPath.Values)
            {
                if (!currentByPath.ContainsKey(RelativeFilePath.Key(previousFile.RelativePath)))
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
            // Пока признак различия — другой размер или другое время записи.
            return
                previousFile.Size != currentFile.Size
                ||
                previousFile.LastWriteTimeUtc != currentFile.LastWriteTimeUtc;
        }

        /// <summary>
        /// Индексирует снимок по общему ключу пути.
        /// </summary>
        private static Dictionary<string, FileSnapshot> Index(IReadOnlyCollection<FileSnapshot> files)
        {
            Dictionary<string, FileSnapshot> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (FileSnapshot file in files)
            {
                if (!result.TryAdd(RelativeFilePath.Key(file.RelativePath), file))
                {
                    throw new ArgumentException("Снимок содержит один путь с разным регистром или разделителем.");
                }
            }

            return result;
        }
    }
}
