using System.ComponentModel;
using System.Windows.Input;
using HwSync.Client.Windows.Contract.Configuration;
using HwSync.Client.Windows.Contract.Diagnostics;

namespace HwSync.Client.Windows.Contract.ViewModels
{
    /// <summary>
    /// Состояние и команды клиента, используемые WPF-хостом.
    /// </summary>
    public interface IMainViewModel : INotifyPropertyChanged, IDisposable
    {
        string ServerAddress { get; set; }

        string RootPath { get; set; }

        string ClientRootPath { get; set; }

        SyncProfile? SelectedProfile { get; set; }

        IReadOnlyList<SyncProfile> Profiles { get; }

        IReadOnlyList<ChangeRow> Changes { get; }

        string Status { get; }

        string Error { get; }

        string JobId { get; }

        string ResultSummary { get; }

        bool HasActiveJob { get; }

        bool CanEditConnection { get; }

        bool HasTransferMetrics { get; }

        string MetricsSummary { get; }

        ITransferMetrics? TransferMetrics { get; }

        Func<string, IReadOnlyList<string>, bool>? ConfirmDeletion { get; set; }

        ICommand ConnectCommand { get; }

        ICommand StartCommand { get; }

        ICommand ResumeCommand { get; }

        ICommand AutoSyncCommand { get; }

        ICommand CopyCommand { get; }

        ICommand UploadCommand { get; }

        ICommand DeleteServerCommand { get; }

        ICommand DeleteClientCommand { get; }

        ICommand CancelCommand { get; }
    }
}
