using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HwSync.Api.Client;
using HwSync.Api.Contracts.Administration;
using HwSync.Windows.Contract.Administration;

namespace HwSync.Windows.AppServices.Administration
{
    /// <summary>
    /// Управляет просмотром настроек, папок и похоронной книги сервера.
    /// </summary>
    public sealed class AdminViewModel : ObservableObject, IAdminViewModel
    {
        private readonly Func<Uri, string, IAdministrationApiClient> _factory;
        private readonly AsyncRelayCommand _connect;
        private readonly AsyncRelayCommand _history;
        private readonly AsyncRelayCommand _more;
        private readonly RelayCommand _cancel;
        private IAdministrationApiClient? _api;
        private CancellationTokenSource? _operation;
        private string _serverAddress = "http://localhost:5080/";
        private string _accessToken = "";
        private string _status = "Введите адрес сервера и персональный ключ доступа.";
        private string? _error;
        private bool _isBusy;
        private bool _disposed;
        private int _revision;
        private FolderRow? _selectedFolder;
        private long? _nextCursor;

        public ObservableCollection<SettingRow> Settings { get; } = new();
        public ObservableCollection<FolderRow> Folders { get; } = new();
        public ObservableCollection<DeletionRow> Deletions { get; } = new();
        public ICommand ConnectCommand => _connect;
        public ICommand LoadHistoryCommand => _history;
        public ICommand LoadMoreCommand => _more;
        public ICommand CancelCommand => _cancel;
        public bool IsIdle => !IsBusy;
        public string Status => _status;
        public string? Error => _error;
        public bool IsBusy => _isBusy;

        public string ServerAddress
        {
            get => _serverAddress;
            set
            {
                if (SetProperty(ref _serverAddress, value))
                {
                    InvalidateConnection();
                }
            }
        }

        public string AccessToken
        {
            set
            {
                if (_accessToken != value)
                {
                    _accessToken = value;
                    InvalidateConnection();
                }
            }
        }

        public FolderRow? SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (SetProperty(ref _selectedFolder, value))
                {
                    _operation?.Cancel();
                    _revision++;
                    Deletions.Clear();
                    _nextCursor = null;
                    SetProperty(ref _error, null, nameof(Error));
                    SetProperty(ref _status, value is null ? "Выберите папку." : "Нажмите «Показать историю».", nameof(Status));
                    NotifyCommands();
                }
            }
        }

        /// <summary>
        /// Принимает фабрику независимого административного подключения.
        /// </summary>
        public AdminViewModel(Func<Uri, string, IAdministrationApiClient> factory)
        {
            _factory = factory;
            _connect = new(() => RunAsync(ConnectAsync), () => IsIdle && !_disposed);
            _history = new(() => RunAsync(token => ReadHistoryAsync(false, token)), CanReadHistory);
            _more = new(() => RunAsync(token => ReadHistoryAsync(true, token)), () => CanReadHistory() && _nextCursor.HasValue);
            _cancel = new(() => _operation?.Cancel(), () => IsBusy);
        }

        /// <summary>
        /// Отменяет запрос и освобождает подключение при закрытии окна.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _revision++;
            _operation?.Cancel();
            _api?.Dispose();
            _api = null;
            _accessToken = "";
            NotifyCommands();
        }

        /// <summary>
        /// Проверяет доступ и обновляет настройки и каталог папок одним результатом.
        /// </summary>
        private async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(ServerAddress, UriKind.Absolute, out Uri? address)
                || address.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(address.UserInfo)
                || !string.IsNullOrEmpty(address.Query) || !string.IsNullOrEmpty(address.Fragment))
            {
                throw new InvalidDataException("Введите HTTP(S)-адрес сервера без пароля, query и fragment.");
            }

            if (_accessToken.Length is < 32 or > 512 || _accessToken.Any(char.IsWhiteSpace))
            {
                throw new InvalidDataException("Введите персональный ключ доступа: от 32 до 512 символов без пробелов.");
            }

            ClearData();
            _api?.Dispose();
            _api = null;
            IAdministrationApiClient? candidate = _factory(new Uri(address.AbsoluteUri.TrimEnd('/') + "/"), _accessToken);
            try
            {
                ServerSettingsDto settings = await candidate.GetSettingsAsync(cancellationToken);
                IReadOnlyList<ServerFolderDto> folders = await candidate.GetFoldersAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                Settings.Add(new("Пользователь управления", settings.UserName));
                Settings.Add(new("Компьютер сервера", settings.MachineName));
                Settings.Add(new("Версия", settings.Version));
                Settings.Add(new("Адреса прослушивания", string.Join(", ", settings.ListeningAddresses)));
                Settings.Add(new("Файл SQLite на сервере", settings.DatabasePath));
                Settings.Add(new("Уровень журналирования", settings.DefaultLogLevel));
                Settings.Add(new("Доступ", settings.LocalConnectionsOnly ? "Только локальные подключения" : "Сетевые подключения"));
                foreach (ServerFolderDto folder in folders)
                {
                    Folders.Add(new(folder.Id, folder.RootPath, folder.FileCount, folder.ActiveDeletionCount));
                }

                _api = candidate;
                candidate = null;
                SetProperty(ref _status, folders.Count == 0
                    ? "Подключено. Известных папок пока нет: выполните сравнение в клиенте синхронизации."
                    : $"Подключено. Папок: {folders.Count}. Выберите папку для просмотра истории.", nameof(Status));
            }
            finally
            {
                candidate?.Dispose();
            }
        }

        /// <summary>
        /// Загружает первую или следующую страницу выбранной похоронной книги.
        /// </summary>
        private async Task ReadHistoryAsync(bool append, CancellationToken cancellationToken)
        {
            IAdministrationApiClient api = _api ?? throw new InvalidOperationException("Нет подключения.");
            FolderRow folder = SelectedFolder ?? throw new InvalidOperationException("Не выбрана папка.");
            long after = append ? _nextCursor ?? 0 : 0;
            if (!append)
            {
                Deletions.Clear();
                _nextCursor = null;
            }

            DeletionPageDto page = await api.GetDeletionsAsync(folder.Id, after, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (DeletionEntryDto entry in page.Entries)
            {
                Deletions.Add(new(entry.Number, entry.RelativePath, entry.OriginParticipantId,
                    entry.DeletedAtUtc.UtcDateTime, entry.PreviousSize / 1_000_000m, entry.PreviousModifiedUtc.UtcDateTime,
                    entry.Active ? "Активная" : "Неактивная"));
            }

            _nextCursor = page.NextCursor;
            SetProperty(ref _status, Deletions.Count == 0 ? "История удалений этой папки пуста."
                : $"Показано записей: {Deletions.Count}." + (_nextCursor.HasValue ? " Доступна следующая страница." : ""), nameof(Status));
        }

        /// <summary>
        /// Выполняет запрос с отменой и сообщением об ошибке, не оставляя занятый интерфейс.
        /// </summary>
        private async Task RunAsync(Func<CancellationToken, Task> action)
        {
            if (IsBusy || _disposed)
            {
                return;
            }

            int revision = _revision;
            using CancellationTokenSource cancellation = new();
            _operation = cancellation;
            SetBusy(true);
            SetProperty(ref _error, null, nameof(Error));
            SetProperty(ref _status, "Загрузка…", nameof(Status));
            try
            {
                await action(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                if (revision == _revision)
                {
                    SetProperty(ref _status, cancellation.IsCancellationRequested
                        ? "Запрос отменён." : "Сервер не ответил за отведённое время.", nameof(Status));
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or JsonException
                or InvalidOperationException or ArgumentException)
            {
                if (revision == _revision)
                {
                    SetProperty(ref _error, exception.Message, nameof(Error));
                    SetProperty(ref _status, "Не удалось загрузить данные.", nameof(Status));
                }
            }
            finally
            {
                _operation = null;
                SetBusy(false);
            }
        }

        /// <summary>
        /// Проверяет возможность чтения журнала.
        /// </summary>
        private bool CanReadHistory() => IsIdle && !_disposed && _api is not null && SelectedFolder is not null;

        /// <summary>
        /// Убирает данные предыдущего подключения при смене адреса или ключа.
        /// </summary>
        private void InvalidateConnection()
        {
            _revision++;
            _operation?.Cancel();
            _api?.Dispose();
            _api = null;
            ClearData();
            SetProperty(ref _error, null, nameof(Error));
            SetProperty(ref _status, "Параметры изменены. Подключитесь к серверу.", nameof(Status));
            NotifyCommands();
        }

        /// <summary>
        /// Очищает снимок интерфейса без изменения серверных данных.
        /// </summary>
        private void ClearData()
        {
            _selectedFolder = null;
            OnPropertyChanged(nameof(SelectedFolder));
            Settings.Clear();
            Folders.Clear();
            Deletions.Clear();
            _nextCursor = null;
        }

        /// <summary>
        /// Обновляет доступность элементов во время запроса.
        /// </summary>
        private void SetBusy(bool value)
        {
            SetProperty(ref _isBusy, value, nameof(IsBusy));
            OnPropertyChanged(nameof(IsIdle));
            NotifyCommands();
        }

        /// <summary>
        /// Уведомляет кнопки об изменении условий выполнения.
        /// </summary>
        private void NotifyCommands()
        {
            _connect.NotifyCanExecuteChanged();
            _history.NotifyCanExecuteChanged();
            _more.NotifyCanExecuteChanged();
            _cancel.NotifyCanExecuteChanged();
        }
    }
}
