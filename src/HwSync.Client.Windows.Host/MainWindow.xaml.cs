using System.ComponentModel;
using System.Windows;
using HwSync.Client.Windows.Contract.ViewModels;

namespace HwSync.Client.Windows.Host
{
    /// <summary>
    /// Окно сравнения папок и ручной синхронизации.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IMainViewModel _viewModel;

        /// <summary>
        /// Связывает окно с моделью и подтверждением удаления.
        /// </summary>
        public MainWindow(IMainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
            viewModel.ConfirmDeletion = ConfirmDeletion;
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
                Margin = new Thickness(8, 0, 0, 0)
            };
            confirm.Click += (_, _) => dialog.DialogResult = true;
            buttons.Children.Add(cancel);
            buttons.Children.Add(confirm);
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

        /// <summary>
        /// Обрабатывает нажатие без дополнительных действий.
        /// </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
