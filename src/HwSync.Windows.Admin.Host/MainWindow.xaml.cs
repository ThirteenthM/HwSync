using System.Windows;
using HwSync.Windows.Contract.Administration;

namespace HwSync.Windows.Admin.Host
{
    /// <summary>
    /// Оболочка просмотра серверных данных с моделью из DI.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IAdminViewModel _model;

        /// <summary>
        /// Подключает модель окна к представлению.
        /// </summary>
        public MainWindow(IAdminViewModel model)
        {
            _model = model;
            InitializeComponent();
            DataContext = model;
        }

        /// <summary>
        /// Передаёт введённый ключ модели без сохранения в конфигурацию.
        /// </summary>
        private void AccessKeyChanged(object sender, RoutedEventArgs e)
        {
            _model.AccessToken = AccessKey.Password;
        }

        /// <summary>
        /// Останавливает запросы при закрытии окна.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _model.Dispose();
            base.OnClosed(e);
        }
    }
}
