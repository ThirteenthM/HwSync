using System.Windows;
using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.Client.Application
{
    /// <summary>
    /// Выбор отдельных копий перед подтверждением удаления.
    /// </summary>
    public partial class DeletionReviewWindow : Window
    {
        private readonly IMainViewModel _model;
        private readonly IReadOnlyList<DeletionSelectionRow> _rows;

        public IReadOnlyList<DeletionCandidate> SelectedFiles => _rows.Where(row => row.IsSelected).Select(row => row.File).ToArray();

        /// <summary>
        /// Создаёт независимое выделение без изменения исходного плана.
        /// </summary>
        public DeletionReviewWindow(IMainViewModel model, IReadOnlyList<DeletionCandidate> files)
        {
            _model = model;
            _rows = files.Select(file => new DeletionSelectionRow(file)).ToArray();
            InitializeComponent();
            FilesGrid.ItemsSource = _rows;
            UpdateSelection();
        }

        /// <summary>
        /// Обновляет счётчик после переключения галочки.
        /// </summary>
        private void SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox checkBox)
            {
                checkBox.GetBindingExpression(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty)?.UpdateSource();
            }

            UpdateSelection();
        }

        /// <summary>
        /// Запрещает подтверждение пустого списка.
        /// </summary>
        private void UpdateSelection()
        {
            if (ConfirmButton is null)
            {
                return;
            }

            int count = _rows.Count(row => row.IsSelected);
            ConfirmButton.IsEnabled = count > 0;
            SelectionSummary.Text = $"Выбрано копий: {count} из {_rows.Count}";
        }

        /// <summary>
        /// Подтверждает только отмеченные копии.
        /// </summary>
        private void ConfirmClick(object sender, RoutedEventArgs e)
        {
            if (SelectedFiles.Count > 0)
            {
                DialogResult = true;
            }
        }

        /// <summary>
        /// Открывает встроенный просмотр одной выбранной версии.
        /// </summary>
        private void PreviewClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: DeletionSelectionRow row })
            {
                new FilePreviewWindow(_model, row.File) { Owner = this }.ShowDialog();
            }
        }
    }

    /// <summary>
    /// Локальная галочка диалога для одной копии файла.
    /// </summary>
    public sealed class DeletionSelectionRow(DeletionCandidate file)
    {
        public DeletionCandidate File { get; } = file;

        public bool IsSelected { get; set; } = true;
    }
}