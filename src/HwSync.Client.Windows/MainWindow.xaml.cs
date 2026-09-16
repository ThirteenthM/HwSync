using System.ComponentModel;
using System.Windows;
using HwSync.Client.Windows.ViewModels;

namespace HwSync.Client.Windows
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
        }

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

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
