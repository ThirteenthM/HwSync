using System.ComponentModel;
using System.Windows;
using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.Client.Application
{
    /// <summary>
    /// Окно сравнения папок и ручной синхронизации.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IMainViewModel _viewModel;

        /// <summary>
        /// Передаёт выбранные пути общей команде редактирования плана.
        /// </summary>
        private void BatchDecisionClick(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem item
                && Enum.TryParse(item.Tag as string, out FileSyncDecision action))
            {
                string[] paths = ResultsGrid.SelectedItems.OfType<ChangeRow>().Select(row => row.Path).ToArray();
                BatchDecisionChoice choice = new(paths, action);
                if (_viewModel.SetBatchDecisionCommand.CanExecute(choice))
                {
                    _viewModel.SetBatchDecisionCommand.Execute(choice);
                }
            }
        }

        /// <summary>
        /// Сбрасывает выделение при смене папки или режима вложенности.
        /// </summary>
        private void ClearFilteredSelection(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(IMainViewModel.SelectedFolderPath) or nameof(IMainViewModel.IncludeSubfolders) or nameof(IMainViewModel.ShowUnchanged))
            {
                ResultsGrid.UnselectAll();
            }
        }
        /// <summary>
        /// Передаёт выбранную папку фильтру модели.
        /// </summary>
        private void FolderSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is IMainViewModel model && e.NewValue is FolderNode folder)
            {
                model.SelectedFolderPath = folder.RelativePath;
            }
        }
        /// <summary>
        /// Связывает окно с моделью и подтверждением удаления.
        /// </summary>
        public MainWindow(IMainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            viewModel.PropertyChanged += ClearFilteredSelection;
            Closed += (_, _) => viewModel.PropertyChanged -= ClearFilteredSelection;
            viewModel.ReviewDeletions = ReviewDeletions;
            viewModel.ChooseConflictResolution = ChooseConflictResolution;
        }

        /// <summary>
        /// Открывает меню действий строки обычным нажатием.
        /// </summary>
        private void DecisionButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.ContextMenu is not null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// Открывает разбор выбранной строки и возвращает решение для плана.
        /// </summary>
        private FileSyncDecision? ChooseConflictResolution(ChangeRow row)
        {
            ConflictWindow dialog = new(row) { Owner = this };
            return dialog.ShowDialog() == true ? dialog.Decision : null;
        }
        /// <summary>
        /// Возвращает выбранные копии либо отмену без изменения плана.
        /// </summary>
        private IReadOnlyList<DeletionCandidate>? ReviewDeletions(IReadOnlyList<DeletionCandidate> files)
        {
            DeletionReviewWindow dialog = new(_viewModel, files) { Owner = this };
            return dialog.ShowDialog() == true ? dialog.SelectedFiles : null;
        }
        /// <summary>
        /// Предупреждает о продолжающемся серверном задании.
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (_viewModel.HasActiveJob && MessageBox.Show(this,
                "Задание продолжит выполняться на сервере. Закрыть клиент? Для отмены задания сначала нажмите «Отменить».",
                "Закрытие клиента", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                e.Cancel = true;
            }
            base.OnClosing(e);
        }
    }
}
