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
            viewModel.ConfirmDeletion = ConfirmDeletion;
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
        /// Показывает список удаляемых файлов и ожидает решение пользователя.
        /// </summary>
        private bool ConfirmDeletion(string side, IReadOnlyList<string> paths)
        {
            Window dialog = new()
            {
                Owner = this,
                Title = "Подтверждение удаления",
                Width = 680,
                Height = 460,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            System.Windows.Controls.DockPanel panel = new()
            {
                Margin = new Thickness(16)
            };
            System.Windows.Controls.TextBlock description = new()
            {
                Text = $"Удалить файлы {side} без помещения в корзину? Всего: {paths.Count}.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            System.Windows.Controls.DockPanel.SetDock(description, System.Windows.Controls.Dock.Top);
            panel.Children.Add(description);
            System.Windows.Controls.StackPanel buttons = new()
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            System.Windows.Controls.Button cancel = new()
            {
                Content = "Отмена",
                IsCancel = true,
                IsDefault = true,
                Padding = new Thickness(16, 8, 16, 8)
            };
            System.Windows.Controls.Button confirm = new()
            {
                Content = "Удалить",
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 8, 0)
            };
            confirm.Click += (_, _) => dialog.DialogResult = true;
            buttons.Children.Add(confirm);
            buttons.Children.Add(cancel);
            System.Windows.Controls.DockPanel.SetDock(buttons, System.Windows.Controls.Dock.Bottom);
            panel.Children.Add(buttons);
            panel.Children.Add(new System.Windows.Controls.ListBox
            {
                ItemsSource = paths,
                Margin = new Thickness(0, 0, 0, 12)
            });
            dialog.Content = panel;
            return dialog.ShowDialog() == true;
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
