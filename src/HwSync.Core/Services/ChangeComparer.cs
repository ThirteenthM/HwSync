using HwSync.Core.Models;

namespace HwSync.Core.Services
{
    public sealed class ChangeComparer
    {
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
                if (
                    !previousByPath.TryGetValue(
                        currentFile.RelativePath,
                        out FileSnapshot? previousFile
                    )
                )
                {
                    changes.Add(
                        new(
                            FileChangeType.Created,
                            Previous: null,
                            Current: currentFile
                        )
                    );

                    continue;
                }

                if (IsModified(previousFile, currentFile))
                {
                    changes.Add(
                        new(
                            FileChangeType.Modified,
                            Previous: previousFile,
                            Current: currentFile
                        )
                    );
                }
            }

            foreach (FileSnapshot previousFile in previousByPath.Values)
            {
                if (!currentByPath.ContainsKey(previousFile.RelativePath))
                {
                    changes.Add(
                        new(
                            FileChangeType.Deleted,
                            Previous: previousFile,
                            Current: null
                        )
                    );
                }
            }

            return changes;
        }

        private static bool IsModified(
            FileSnapshot previous,
            FileSnapshot current
        )
        {
            return previous.Size != current.Size
                || previous.LastWriteTimeUtc != current.LastWriteTimeUtc;
        }
    }
}
