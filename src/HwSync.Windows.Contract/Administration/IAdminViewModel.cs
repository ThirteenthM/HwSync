using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace HwSync.Windows.Contract.Administration
{
    /// <summary>
    /// Контракт окна администрирования без зависимости от WPF.
    /// </summary>
    public interface IAdminViewModel : INotifyPropertyChanged, IDisposable
    {
        string ServerAddress { get; set; }
        string AccessToken { set; }
        string Status { get; }
        string? Error { get; }
        bool IsBusy { get; }
        bool IsIdle { get; }
        FolderRow? SelectedFolder { get; set; }
        ObservableCollection<SettingRow> Settings { get; }
        ObservableCollection<FolderRow> Folders { get; }
        ObservableCollection<DeletionRow> Deletions { get; }
        ICommand ConnectCommand { get; }
        ICommand LoadHistoryCommand { get; }
        ICommand LoadMoreCommand { get; }
        ICommand CancelCommand { get; }
    }
}
