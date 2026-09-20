using System.Windows;
using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.Client.Host
{
    /// <summary>
    /// Просмотр атрибутов конфликтующих версий и выбор решения.
    /// </summary>
    public partial class ConflictWindow : Window
    {
        private readonly FileConflictKind _kind;

        public FileSyncDecision Decision { get; private set; }

        /// <summary>
        /// Показывает данные сравнения и текущее решение строки.
        /// </summary>
        public ConflictWindow(ChangeRow row)
        {
            InitializeComponent();
            DataContext = row;
            Decision = row.Action;
            _kind = row.ConflictKind;
            if (row.IsDeletionConflict)
            {
                KeepBoth.Visibility = Visibility.Collapsed;
                TakeServer.Content = _kind == FileConflictKind.DeletedOnServer
                    ? "Подтвердить удаление — удалить оставшуюся копию на клиенте"
                    : "Восстановить файл на клиенте из серверной копии";
                TakeClient.Content = _kind == FileConflictKind.DeletedOnClient
                    ? "Подтвердить удаление — удалить оставшуюся копию на сервере"
                    : "Восстановить файл на сервере из клиентской копии";
                if (row.Action == FileSyncDecision.Skip)
                {
                    LeaveUnchanged.IsChecked = true;
                }
                else
                {
                    DecideLater.IsChecked = true;
                }

                return;
            }
            switch (row.Action)
            {
                case FileSyncDecision.ReplaceOnClient:
                    TakeServer.IsChecked = true;
                    break;
                case FileSyncDecision.ReplaceOnServer:
                    TakeClient.IsChecked = true;
                    break;
                case FileSyncDecision.Skip:
                    LeaveUnchanged.IsChecked = true;
                    break;
                default:
                    KeepBoth.IsChecked = true;
                    break;
            }
        }

        /// <summary>
        /// Возвращает решение без выполнения файловых операций.
        /// </summary>
        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            FileSyncDecision serverChoice = _kind switch
            {
                FileConflictKind.DeletedOnServer => FileSyncDecision.DeleteOnClient,
                FileConflictKind.DeletedOnClient => FileSyncDecision.CopyToClient,
                _ => FileSyncDecision.ReplaceOnClient
            };
            FileSyncDecision clientChoice = _kind switch
            {
                FileConflictKind.DeletedOnClient => FileSyncDecision.DeleteOnServer,
                FileConflictKind.DeletedOnServer => FileSyncDecision.CopyToServer,
                _ => FileSyncDecision.ReplaceOnServer
            };
            Decision = TakeServer.IsChecked == true ? serverChoice
                : TakeClient.IsChecked == true ? clientChoice
                : LeaveUnchanged.IsChecked == true ? FileSyncDecision.Skip
                : DecideLater.IsChecked == true ? FileSyncDecision.AskUser
                : FileSyncDecision.KeepBoth;
            DialogResult = true;
        }
    }
}
