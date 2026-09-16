using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;

namespace HwSync.Infrastructure.FileSystem
{
    public sealed class JsonFolderHistory : IFolderHistory
    {
        private readonly string _directory;
        private readonly object _gate = new();
        public JsonFolderHistory(string directory) { _directory = Path.GetFullPath(directory); }

        public void RecordSnapshot(string rootPath, IReadOnlyCollection<FileSnapshot> snapshot)
        {
            lock (_gate)
            {
                string path = GetStatePath(rootPath);
                FolderState previous = Read(path);
                Dictionary<string, FileSnapshot> current = snapshot.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
                List<DeletedFile> deleted = previous.DeletedFiles.ToList();
                long number = previous.ChangeNumber;
                foreach (FileSnapshot file in previous.Files)
                {
                    if (!current.ContainsKey(file.RelativePath))
                    {
                        deleted.Add(new(file.RelativePath, true, DateTimeOffset.UtcNow, ++number, file));
                    }
                }
                // При повторном появлении файла история остаётся, но запись больше не активна.
                deleted = deleted.Select(file => file.Deleted && current.ContainsKey(file.RelativePath)
                    ? file with { Deleted = false } : file).ToList();
                FolderState next = new(number, snapshot.ToArray(), deleted.ToArray());
                Directory.CreateDirectory(_directory);
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true }));
                    File.Move(temporary, path, true);
                }
                finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
            }
        }

        public IReadOnlyList<DeletedFile> GetDeletedFiles(string rootPath)
        {
            lock (_gate) { return Read(GetStatePath(rootPath)).DeletedFiles; }
        }

        private string GetStatePath(string rootPath)
        {
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
            if (_directory.Equals(root, StringComparison.OrdinalIgnoreCase)
                || _directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Хранилище метаданных должно находиться вне синхронизируемой папки.");
            }
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToUpperInvariant())));
            return Path.Combine(_directory, key + ".json");
        }

        private static FolderState Read(string path) => File.Exists(path)
            ? JsonSerializer.Deserialize<FolderState>(File.ReadAllText(path)) ?? throw new InvalidDataException("Некорректная история папки.")
            : new(0, [], []);

        public sealed record FolderState(long ChangeNumber, FileSnapshot[] Files, DeletedFile[] DeletedFiles);
    }
}
