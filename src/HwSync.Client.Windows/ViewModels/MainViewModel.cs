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
    public sealed class MainViewModel : ObservableObject, IDisposable
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
        public IReadOnlyList<SyncProfile> Profiles { get; private set; } = [];
        public SyncProfile? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (!CanEditConnection || !SetProperty(ref _selectedProfile, value) || value is null)
                { return; }
                ServerAddress = value.ServerAddress;
                RootPath = value.ServerRootPath;
                ClientRootPath = value.ClientRootPath;
            }
        }
        public void LoadProfiles(IReadOnlyList<SyncProfile> profiles)
        {
            Profiles = profiles;
            OnPropertyChanged(nameof(Profiles));
            SelectedProfile = profiles.FirstOrDefault();
        }
        private Guid? _activeJob;
        private string _serverAddress = "http://localhost:5080";
        private string _rootPath = "";
        private string _status = "Готов к подключению";
        private string _error = "";
        private string _jobId = "—";
        private bool _busy;
        private bool _cancelRequested;
        private IReadOnlyList<ChangeRow> _changes = Array.Empty<ChangeRow>();

        public MainViewModel(Func<Uri, IHwSyncApiClient> createClient, IFileSnapshotProvider snapshotProvider)
        {
            _createClient = createClient;
            _snapshotProvider = snapshotProvider;
            ConnectCommand = new AsyncRelayCommand(ConnectAsync, () => !_busy && !HasActiveJob);
            StartCommand = new AsyncRelayCommand(StartAsync, () => !_busy && !HasActiveJob && !string.IsNullOrWhiteSpace(RootPath) && !string.IsNullOrWhiteSpace(ClientRootPath));
            ResumeCommand = new AsyncRelayCommand(ResumeAsync, () => !_busy && HasActiveJob);
            CopyCommand = new AsyncRelayCommand(CopyMissingAsync, () => !_busy && _completedComparison is not null && _jobClient is IFileDownloadClient);
            UploadCommand = new AsyncRelayCommand(() => ApplyManualAsync("upload"), CanMutate);
            DeleteServerCommand = new AsyncRelayCommand(() => ApplyManualAsync("delete-server"), CanMutate);
            DeleteClientCommand = new AsyncRelayCommand(() => ApplyManualAsync("delete-client"), CanMutate);
            CancelCommand = new AsyncRelayCommand(CancelAsync, () => (_copyCancellation is not null || HasActiveJob) && !_cancelRequested);
        }

        public string ServerAddress { get => _serverAddress; set { if (SetProperty(ref _serverAddress, value)) { _completedComparison = null; RefreshCommands(); } } }
        public string ClientRootPath { get => _clientRootPath; set { if (SetProperty(ref _clientRootPath, value)) { _completedComparison = null; } RefreshCommands(); } }
        public string RootPath { get => _rootPath; set { if (SetProperty(ref _rootPath, value)) { _completedComparison = null; } RefreshCommands(); } }
        public string Status { get => _status; private set => SetProperty(ref _status, value); }
        public string Error { get => _error; private set => SetProperty(ref _error, value); }
        public string JobId { get => _jobId; private set => SetProperty(ref _jobId, value); }
        public IReadOnlyList<ChangeRow> Changes { get => _changes; private set { SetProperty(ref _changes, value); OnPropertyChanged(nameof(ResultSummary)); } }
        public string ResultSummary => $"Найдено различий: {Changes.Count}";
        public bool HasActiveJob => _activeJob.HasValue;
        public bool CanEditConnection => !_busy && !HasActiveJob;
        public Func<string, IReadOnlyList<string>, bool>? ConfirmDeletion { get; set; }
        public IAsyncRelayCommand UploadCommand { get; }
        public IAsyncRelayCommand DeleteServerCommand { get; }
        public IAsyncRelayCommand DeleteClientCommand { get; }
        private bool CanMutate() => !_busy && _completedComparison is not null && _jobClient is IFileMutationClient;
        public IAsyncRelayCommand CopyCommand { get; }
        public IAsyncRelayCommand ConnectCommand { get; }
        public IAsyncRelayCommand StartCommand { get; }
        public IAsyncRelayCommand ResumeCommand { get; }
        public IAsyncRelayCommand CancelCommand { get; }

        private IHwSyncApiClient CreateClient()
        {
            if (!Uri.TryCreate(ServerAddress.Trim(), UriKind.Absolute, out Uri? address))
            {
                throw new ArgumentException("Укажите адрес сервера, например http://localhost:5080.");
            }
            return _createClient(address);
        }

        private async Task ConnectAsync()
        {
            await ExecuteAsync(async () =>
            {
                Status = "Проверка подключения…";
                HealthResponse health = await CreateClient().GetHealthAsync(_lifetime.Token);
                if (!string.Equals(health.Status, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Сервер не подтвердил готовность.");
                }
                Status = "Сервер доступен";
            });
        }

        private async Task StartAsync()
        {
            await ExecuteAsync(async () =>
            {
                _completedComparison = null;
                IHwSyncApiClient client = CreateClient();
                string clientRoot = ClientRootPath.Trim();
                string serverRoot = RootPath.Trim();
                if (!Path.IsPathFullyQualified(clientRoot))
                {
                    throw new ArgumentException("Укажите абсолютный путь папки клиента.");
                }
                if (!Path.IsPathFullyQualified(serverRoot))
                {
                    throw new ArgumentException("Укажите абсолютный путь папки сервера.");
                }
                Status = "Чтение папки клиента…";
                Changes = Array.Empty<ChangeRow>();
                JobId = "—";
                IReadOnlyCollection<FileSnapshot> snapshot = await Task.Run(
                    () => _snapshotProvider.GetSnapshot(clientRoot), _lifetime.Token);
                _lifetime.Token.ThrowIfCancellationRequested();
                FileSnapshotDto[] clientSnapshot = [.. snapshot.Select(file => new FileSnapshotDto(file.RelativePath, file.Size, file.LastWriteTimeUtc))];
                Status = "Отправка снимка и запуск сравнения…";
                ScanJobResponse job;
                try
                {
                    job = await client.StartComparisonAsync(new(serverRoot, clientSnapshot), _lifetime.Token);
                }
                catch (Exception exception) when (exception is HttpRequestException and not HwSyncApiException or OperationCanceledException or InvalidDataException)
                {
                    throw new InvalidOperationException("Не удалось подтвердить запуск. Задание могло быть создано на сервере; автоматического повторного запуска нет.", exception);
                }
                _comparedClientRoot = clientRoot;
                _jobClient = client;
                _activeJob = job.Id;
                _cancelRequested = false;
                JobId = job.Id.ToString();
                ApplyJob(job);
                await PollAsync();
            });
        }

        private async Task CopyMissingAsync()
        {
            ScanJobResponse? comparison = _completedComparison;
            if (comparison is null || _jobClient is not IFileDownloadClient downloader)
            { return; }
            await ExecuteAsync(async () =>
            {
                _completedComparison = null;
                _cancelRequested = false;
                using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                _copyCancellation = cancellation;
                RefreshCommands();
                try
                {
                    FileSnapshot[] files = comparison.Changes!.Where(change => change.ChangeType == FileChangeKind.Created)
                        .Select(change => new FileSnapshot(change.Current!.RelativePath, change.Current.Size, change.Current.LastWriteTimeUtc)).ToArray();
                    Status = $"Копирование отсутствующих файлов: {files.Length}…";
                    MissingFileSynchronizer synchronizer = new();
                    IReadOnlyList<FileCopyResult> results = await synchronizer.CopyAsync(_comparedClientRoot, files,
                        (file, stream, token) => downloader.DownloadFileAsync(comparison.Id, file.RelativePath, stream, token), cancellation.Token);
                    Status = $"Скопировано: {results.Count(result => result.Copied)}. Пропущено или с ошибкой: {results.Count(result => !result.Copied)}. Повторите сравнение.";
                    Error = string.Join("\n", results.Where(result => !result.Copied).Take(3).Select(result => result.RelativePath + ": " + result.Error));
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                { Status = "Копирование отменено. Уже полученные файлы сохранены. Повторите сравнение."; }
                finally { _copyCancellation = null; RefreshCommands(); }
            });
        }
        private async Task ApplyManualAsync(string operation)
        {
            ScanJobResponse? comparison = _completedComparison;
            if (comparison is null || _jobClient is not IFileMutationClient remote)
            { return; }
            FileChangeKind kind = operation == "delete-server" ? FileChangeKind.Created : FileChangeKind.Deleted;
            FileSnapshot[] files = comparison.Changes!.Where(change => change.ChangeType == kind)
                .Select(change => kind == FileChangeKind.Created ? change.Current! : change.Previous!)
                .Select(file => new FileSnapshot(file.RelativePath, file.Size, file.LastWriteTimeUtc)).ToArray();
            if (files.Length == 0)
            { Status = "Для выбранного действия нет файлов."; return; }
            if (operation != "upload" && ConfirmDeletion?.Invoke(
                operation == "delete-server" ? "на сервере" : "на клиенте",
                files.Select(file => file.RelativePath).ToArray()) != true)
            { return; }
            await ExecuteAsync(async () =>
            {
                _completedComparison = null;
                _cancelRequested = false;
                using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                _copyCancellation = cancellation;
                RefreshCommands();
                int completed = 0;
                try
                {
                    IComparedFileOperations local = new ComparedFileOperations();
                    ISourceFileReader reader = new SourceFileReader();
                    foreach (FileSnapshot file in files)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        Status = $"Обработка {completed + 1} из {files.Length}: {file.RelativePath}";
                        if (operation == "upload")
                        {
                            await using Stream source = reader.OpenRead(_comparedClientRoot, file);
                            await remote.UploadFileAsync(comparison.Id, file.RelativePath, source, cancellation.Token);
                        }
                        else if (operation == "delete-server")
                        {
                            local.EnsureMissing(_comparedClientRoot, file.RelativePath);
                            await remote.DeleteServerFileAsync(comparison.Id, file.RelativePath, cancellation.Token);
                        }
                        else
                        {
                            await remote.EnsureServerFileMissingAsync(comparison.Id, file.RelativePath, cancellation.Token);
                            cancellation.Token.ThrowIfCancellationRequested();
                            local.DeleteUnchanged(_comparedClientRoot, file);
                        }
                        completed++;
                    }
                    Status = $"Выполнено: {completed}. Повторите сравнение.";
                }
                catch (Exception exception) when (exception is IOException or HttpRequestException or UnauthorizedAccessException or OperationCanceledException)
                {
                    Status = $"Операция остановлена. Выполнено: {completed} из {files.Length}. Повторите сравнение.";
                    Error = exception is OperationCanceledException
                        ? "Уже выполненные действия сохранены. Результат последнего запроса проверьте сравнением."
                        : DescribeError(exception);
                }
                finally { _copyCancellation = null; RefreshCommands(); }
            });
        }
        private Task ResumeAsync() => ExecuteAsync(PollAsync);

        private async Task PollAsync()
        {
            while (_activeJob is Guid id && _jobClient is not null)
            {
                ScanJobResponse job = await _jobClient.GetScanAsync(id, _lifetime.Token);
                if (_activeJob == id)
                { ApplyJob(job); }
                if (HasActiveJob)
                { await Task.Delay(750, _lifetime.Token); }
            }
        }

        private async Task CancelAsync()
        {
            if (_copyCancellation is not null)
            { _copyCancellation.Cancel(); return; }
            if (_activeJob is not Guid id || _jobClient is null)
            { return; }
            _cancelRequested = true;
            RefreshCommands();
            try
            {
                ScanJobResponse job = await _jobClient.CancelScanAsync(id, _lifetime.Token);
                if (_activeJob == id)
                { ApplyJob(job); }
            }
            catch (Exception exception)
            {
                if (!_lifetime.IsCancellationRequested)
                {
                    _cancelRequested = false;
                    Error = DescribeError(exception);
                }
            }
            finally { RefreshCommands(); }
        }

        private void ApplyJob(ScanJobResponse job)
        {
            // Ответ опроса, отправленного до отмены, не должен вернуть интерфейс в Running.
            if (_cancelRequested && job.Status is ScanJobState.Queued or ScanJobState.Running)
            {
                Status = "Отмена запрошена, ожидаем сервер…";
                return;
            }
            Status = job.Status switch
            {
                ScanJobState.Queued => "В очереди",
                ScanJobState.Running => "Сканирование…",
                ScanJobState.CancellationRequested => "Отмена запрошена, ожидаем завершения чтения…",
                ScanJobState.Completed => "Сравнение завершено",
                ScanJobState.Cancelled => "Задание отменено",
                ScanJobState.Failed => "Ошибка сканирования",
                _ => "Неизвестное состояние"
            };
            if (job.Status is ScanJobState.Completed or ScanJobState.Cancelled or ScanJobState.Failed)
            {
                _completedComparison = job.Status == ScanJobState.Completed ? job : null;
                _activeJob = null;
                Changes = job.Changes?.Select(ChangeRow.FromDto).ToArray() ?? [];
                Error = job.Error ?? "";
            }
            RefreshCommands();
        }

        private async Task ExecuteAsync(Func<Task> action)
        {
            _busy = true;
            Error = "";
            RefreshCommands();
            try
            { await action(); }
            catch (Exception exception)
            {
                if (!_lifetime.IsCancellationRequested)
                {
                    if (exception is HwSyncApiException { StatusCode: HttpStatusCode.NotFound })
                    { _activeJob = null; }
                    Status = HasActiveJob ? "Опрос остановлен. Нажмите «Продолжить опрос»." : "Операция не выполнена";
                    Error = DescribeError(exception);
                }
            }
            finally { _busy = false; RefreshCommands(); }
        }

        private static string DescribeError(Exception exception) => exception switch
        {
            HwSyncApiException api => api.Message,
            HttpRequestException => "Нет соединения с сервером. Проверьте адрес и запуск Host.",
            OperationCanceledException => "Сервер не ответил вовремя. Проверьте соединение.",
            _ => exception.Message
        };

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

        public void Dispose()
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
