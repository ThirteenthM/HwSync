using System.ComponentModel;
using System.Windows.Input;
using HwSync.Windows.Contract.Client.Configuration;
using HwSync.Windows.Contract.Client.Diagnostics;

namespace HwSync.Windows.Contract.Client.ViewModels
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

        IReadOnlyList<ChangeRow> VisibleChanges { get; }

        IReadOnlyList<FolderNode> Folders { get; }

        string SelectedFolderPath { get; set; }

        bool IncludeSubfolders { get; set; }

        bool ShowUnchanged { get; set; }

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

        Func<IReadOnlyList<DeletionCandidate>, IReadOnlyList<DeletionCandidate>?>? ReviewDeletions { get; set; }

        /// <summary>
        /// Читает небольшую копию файла из текущего сравнения для просмотра.
        /// </summary>
        Task<byte[]> LoadDeletionPreviewAsync(DeletionCandidate candidate, CancellationToken token);

        ICommand ConnectCommand { get; }

        ICommand StartCommand { get; }

        ICommand ResumeCommand { get; }

        Func<ChangeRow, FileSyncDecision?>? ChooseConflictResolution { get; set; }

        ICommand ResolveConflictCommand { get; }

        bool CanEditPlan { get; }

        ICommand SetFileDecisionCommand { get; }

        ICommand SetBatchDecisionCommand { get; }

        ICommand AutoSyncCommand { get; }

        ICommand CopyCommand { get; }

        ICommand UploadCommand { get; }

        ICommand DeleteServerCommand { get; }

        ICommand DeleteClientCommand { get; }

        ICommand CancelCommand { get; }
    }
}
