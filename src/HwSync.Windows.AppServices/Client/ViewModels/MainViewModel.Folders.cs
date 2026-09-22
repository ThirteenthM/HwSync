using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.AppServices.Client.ViewModels
{
    /// <summary>
    /// Навигация по папкам без изменения полного плана синхронизации.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private string _selectedFolderPath = "";
        private bool _includeSubfolders = true;
        private bool _showUnchanged = true;

        public bool ShowUnchanged
        {
            get => _showUnchanged;
            set
            {
                if (SetProperty(ref _showUnchanged, value))
                {
                    RefreshVisibleChanges();
                }
            }
        }
        private string[] _folderPaths = [];

        public IReadOnlyList<FolderNode> Folders { get; private set; } = [];

        public IReadOnlyList<ChangeRow> VisibleChanges { get; private set; } = [];

        public string SelectedFolderPath
        {
            get => _selectedFolderPath;
            set
            {
                if (SetProperty(ref _selectedFolderPath, value))
                {
                    RefreshVisibleChanges();
                }
            }
        }

        public bool IncludeSubfolders
        {
            get => _includeSubfolders;
            set
            {
                if (SetProperty(ref _includeSubfolders, value))
                {
                    RefreshVisibleChanges();
                }
            }
        }

        /// <summary>
        /// Перестраивает дерево только при изменении состава папок.
        /// </summary>
        private void RefreshFolders()
        {
            SortedSet<string> paths = new(StringComparer.Ordinal);
            paths.Add("");
            foreach (ChangeRow row in Changes)
            {
                string folder = row.RelativeFolder;
                while (folder.Length > 0)
                {
                    paths.Add(folder);
                    int separator = folder.LastIndexOf('/');
                    folder = separator < 0 ? "" : folder[..separator];
                }
            }

            string[] current = paths.ToArray();
            if (!_folderPaths.SequenceEqual(current))
            {
                _folderPaths = current;
                _selectedFolderPath = "";
                Folders = [CreateFolderNode("", current)];
                OnPropertyChanged(nameof(Folders));
                OnPropertyChanged(nameof(SelectedFolderPath));
            }

            RefreshVisibleChanges();
        }

        /// <summary>
        /// Создаёт узел с непосредственными дочерними папками.
        /// </summary>
        private static FolderNode CreateFolderNode(string path, IReadOnlyList<string> paths)
        {
            string prefix = path.Length == 0 ? "" : path + "/";
            FolderNode[] children = paths.Where(candidate => candidate.Length > path.Length
                && candidate.StartsWith(prefix, StringComparison.Ordinal)
                && !candidate[prefix.Length..].Contains('/'))
                .Select(candidate => CreateFolderNode(candidate, paths)).ToArray();
            string name = path.Length == 0 ? "Все папки" : path[(path.LastIndexOf('/') + 1)..];
            return new(name, path, children);
        }

        /// <summary>
        /// Обновляет только отображаемые строки, сохраняя решения полного плана.
        /// </summary>
        private void RefreshVisibleChanges()
        {
            VisibleChanges = Changes.Where(row => ShowUnchanged || !row.IsUnchanged).Where(row => row.RelativeFolder == SelectedFolderPath
                || IncludeSubfolders && (SelectedFolderPath.Length == 0
                    || row.RelativeFolder.StartsWith(SelectedFolderPath + "/", StringComparison.Ordinal))).ToArray();
            OnPropertyChanged(nameof(VisibleChanges));
            OnPropertyChanged(nameof(ResultSummary));
        }
    }
}