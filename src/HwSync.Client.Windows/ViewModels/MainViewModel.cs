using HwSync.Client.Windows.Configuration;
using HwSync.Infrastructure.FileSystem;
using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using System.IO;
using System.Net;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HwSync.Api.Client;
using HwSync.Api.Contracts;

namespace HwSync.Client.Windows.ViewModels
{
    /// <summary>Состояние формы и управление командами сравнения и синхронизации.</summary>
    public sealed partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly Func<Uri, IHwSyncApiClient> _createClient;
        private readonly IFileSnapshotProvider _snapshotProvider;
        private string _clientRootPath = string.Empty;
        private readonly CancellationTokenSource _lifetime = new();
        private IHwSyncApiClient? _jobClient;
        private ScanJobResponse? _completedComparison;
        private string _comparedClientRoot = "";
        private SyncProfile? _selectedProfile;
        private CancellationTokenSource? _copyCancellation;
        private Guid? _activeJob;
        private string _serverAddress = "http://localhost:5080";
        private string _rootPath = "";
        private string _status = "Готов к подключению";
        private string _error = "";
        private string _jobId = "—";
        private bool _busy;
        private bool _cancelRequested;
        private IReadOnlyList<ChangeRow> _changes = Array.Empty<ChangeRow>();

        /// <summary>Связывает команды формы с API и чтением локальных снимков.</summary>
        public MainViewModel(Func<Uri, IHwSyncApiClient> createClient, IFileSnapshotProvider snapshotProvider)
        {
            _createClient = createClient;
            _snapshotProvider = snapshotProvider;
            ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => !_busy && !HasActiveJob);
            StartCommand = new AsyncRelayCommand(StartAsync, () => !_busy && !HasActiveJob && !string.IsNullOrWhiteSpace(RootPath) && !string.IsNullOrWhiteSpace(ClientRootPath));
            ResumeCommand = new AsyncRelayCommand(ResumeAsync, () => !_busy && HasActiveJob);
            CopyCommand = new AsyncRelayCommand(CopyMissingAsync, () => !_busy && _completedComparison is not null && _jobClient is IFileDownloadClient);
            UploadCommand = new AsyncRelayCommand(() => ApplyManualAsync(ManualFileOperation.Upload), CanMutate);
            DeleteServerCommand = new AsyncRelayCommand(() => ApplyManualAsync(ManualFileOperation.DeleteServer), CanMutate);
            DeleteClientCommand = new AsyncRelayCommand(() => ApplyManualAsync(ManualFileOperation.DeleteClient), CanMutate);
            CancelCommand = new AsyncRelayCommand(CancelAsync, () => (_copyCancellation is not null || HasActiveJob) && !_cancelRequested);
        }

        public IReadOnlyList<SyncProfile> Profiles
        {
            get;
            private set;
        } = [];

        public SyncProfile? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (!CanEditConnection || !SetProperty(ref _selectedProfile, value) || value is null)
                {
                    return;
                }

                ServerAddress = value.ServerAddress;
                RootPath = value.ServerRootPath;
                ClientRootPath = value.ClientRootPath;
            }
        }

        public string ServerAddress
        {
            get => _serverAddress;
            set
            {
                if (SetProperty(ref _serverAddress, value))
                {
                    _completedComparison = null;
                    RefreshCommands();
                }
            }
        }

        public string ClientRootPath
        {
            get => _clientRootPath;
            set
            {
                if (SetProperty(ref _clientRootPath, value))
                {
                    _completedComparison = null;
                }

                RefreshCommands();
            }
        }

        public string RootPath
        {
            get => _rootPath;
            set
            {
                if (SetProperty(ref _rootPath, value))
                {
                    _completedComparison = null;
                }

                RefreshCommands();
            }
        }

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }
        public string Error
        {
            get => _error;
            private set => SetProperty(ref _error, value);
        }
        public string JobId
        {
            get => _jobId;
            private set => SetProperty(ref _jobId, value);
        }

        public IReadOnlyList<ChangeRow> Changes
        {
            get => _changes;
            private set
            {
                SetProperty(ref _changes, value);
                OnPropertyChanged(nameof(ResultSummary));
            }
        }

        public string ResultSummary => $"Найдено различий: {Changes.Count}";
        public bool HasActiveJob => _activeJob.HasValue;
        public bool CanEditConnection => !_busy && !HasActiveJob;
        public Func<string, IReadOnlyList<string>, bool>? ConfirmDeletion
        {
            get;
            set;
        }
        public IAsyncRelayCommand UploadCommand
        {
            get;
        }
        public IAsyncRelayCommand DeleteServerCommand
        {
            get;
        }
        public IAsyncRelayCommand DeleteClientCommand
        {
            get;
        }
        public IAsyncRelayCommand CopyCommand
        {
            get;
        }
        public IAsyncRelayCommand ConnectCommand
        {
            get;
        }
        public IAsyncRelayCommand StartCommand
        {
            get;
        }
        public IAsyncRelayCommand ResumeCommand
        {
            get;
        }
        public IAsyncRelayCommand CancelCommand
        {
            get;
        }

        /// <summary>Заполняет список профилей и выбирает первый.</summary>
        public void LoadProfiles(IReadOnlyList<SyncProfile> profiles)
        {
            Profiles = profiles;
            OnPropertyChanged(nameof(Profiles));
            SelectedProfile = profiles.FirstOrDefault();
        }

        /// <summary>Проверяет готовность результата сравнения к файловым операциям.</summary>
        private bool CanMutate() => !_busy && _completedComparison is not null && _jobClient is IFileMutationClient;

        /// <summary>Отменяет передачу файлов либо запрашивает отмену задания.</summary>
        private async Task CancelAsync()
        {
            if (_copyCancellation is not null)
            {
                _copyCancellation.Cancel();
                return;
            }

            if (_activeJob is not Guid id || _jobClient is null)
            {
                return;
            }

            _cancelRequested = true;
            RefreshCommands();
            try
            {
                ScanJobResponse job = await _jobClient.CancelScanAsync(id, _lifetime.Token);
                if (_activeJob == id)
                {
                    ApplyJob(job);
                }
            }
            catch (Exception exception)
            {
                if (!_lifetime.IsCancellationRequested)
                {
                    _cancelRequested = false;
                    Error = DescribeError(exception);
                }
            }
            finally
            {
                RefreshCommands();
            }
        }

        /// <summary>Выполняет команду с обновлением занятости и обработкой ошибок.</summary>
        private async Task ExecuteAsync(Func<Task> action)
        {
            _busy = true;
            Error = "";
            RefreshCommands();
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                if (!_lifetime.IsCancellationRequested)
                {
                    if (exception is HwSyncApiException
                        {
                            StatusCode: HttpStatusCode.NotFound
                        })
                    {
                        _activeJob = null;
                    }

                    Status = HasActiveJob ? "Опрос остановлен. Нажмите «Продолжить опрос»." : "Операция не выполнена";
                    Error = DescribeError(exception);
                }
            }
            finally
            {
                _busy = false;
                RefreshCommands();
            }
        }

        /// <summary>Преобразует исключение в сообщение для пользователя.</summary>
        private static string DescribeError(Exception exception) => exception switch
        {
            HwSyncApiException api => api.Message,
            HttpRequestException => "Нет соединения с сервером. Проверьте адрес и запуск Host.",
            OperationCanceledException => "Сервер не ответил вовремя. Проверьте соединение.",
            _ => exception.Message
        };

        /// <summary>Обновляет доступность команд и редактирования формы.</summary>
        private void RefreshCommands()
        {
            UploadCommand.NotifyCanExecuteChanged();
            DeleteServerCommand.NotifyCanExecuteChanged();
            DeleteClientCommand.NotifyCanExecuteChanged();
            CopyCommand.NotifyCanExecuteChanged();
            ConnectCommand.NotifyCanExecuteChanged();
            StartCommand.NotifyCanExecuteChanged();
            ResumeCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasActiveJob));
            OnPropertyChanged(nameof(CanEditConnection));
        }

        /// <summary>Отменяет работу модели и освобождает источник отмены.</summary>
        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
