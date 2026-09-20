using HwSync.Windows.Contract.Client.ViewModels;
using HwSync.Windows.Contract.Client.Configuration;
using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using System.IO;
using System.Net;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HwSync.Api.Client;
using HwSync.Api.Contracts;

namespace HwSync.Windows.AppServices.Client.ViewModels
{
    /// <summary>
    /// Состояние формы и управление командами сравнения и синхронизации.
    /// </summary>
    public sealed partial class MainViewModel : ObservableObject, IDisposable
    {
        /// <summary>
        /// Создаёт API-клиент для адреса из формы.
        /// </summary>
        private IHwSyncApiClient CreateClient()
        {
            if (!Uri.TryCreate(ServerAddress.Trim(), UriKind.Absolute, out Uri? address))
            {
                throw new ArgumentException("Укажите адрес сервера, например http://localhost:5080.");
            }

            return _createClient(address);
        }

        /// <summary>
        /// Проверяет готовность сервера и обновляет состояние формы.
        /// </summary>
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

        /// <summary>
        /// Снимает состояние клиента и запускает сравнение на сервере.
        /// </summary>
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
                IReadOnlyCollection<FileSnapshot> snapshot = await Task.Run(() => _snapshotProvider.GetSnapshot(clientRoot), _lifetime.Token);
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
                await ApplyJobAsync(job);
                await PollAsync();
            });
        }

        /// <summary>
        /// Возобновляет опрос существующего задания.
        /// </summary>
        private Task ResumeAsync() => ExecuteAsync(PollAsync);

        /// <summary>
        /// Опрашивает сервер до завершения активного задания.
        /// </summary>
        private async Task PollAsync()
        {
            while (_activeJob is Guid id && _jobClient is not null)
            {
                ScanJobResponse job = await _jobClient.GetScanAsync(id, _lifetime.Token);
                if (_activeJob == id)
                {
                    await ApplyJobAsync(job);
                }

                if (HasActiveJob)
                {
                    await Task.Delay(750, _lifetime.Token);
                }
            }
        }

        /// <summary>
        /// Отображает состояние задания и сохраняет завершённое сравнение.
        /// </summary>
        private async Task ApplyJobAsync(ScanJobResponse job)
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
                IReadOnlyList<ChangeRow> rows = [];
                if (job.Status == ScanJobState.Completed)
                {
                    // План становится доступным только после успешного чтения обеих историй.
                    Status = "Чтение истории удалений…";
                    if (_history is not null && _jobClient is not IDeletionHistoryClient)
                    {
                        throw new InvalidDataException("Серверный клиент не поддерживает историю удалений. Синхронизация заблокирована.");
                    }

                    IReadOnlyList<DeletedFileDto> remote = _jobClient is IDeletionHistoryClient historyClient
                        ? await historyClient.GetDeletedFilesAsync(job.Id, _lifetime.Token) : [];
                    IReadOnlyList<DeletedFile> local = _history?.GetDeletedFiles(_comparedClientRoot) ?? [];
                    Dictionary<string, FileDeletionEvidence> serverDeleted = remote.GroupBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.OrderByDescending(file => file.ChangeNumber).First()).Where(file => file.Deleted)
                        .ToDictionary(file => file.RelativePath, file => new FileDeletionEvidence(file.RelativePath,
                            file.PreviousFile is null ? null : ToSnapshot(file.PreviousFile)), StringComparer.OrdinalIgnoreCase);
                    Dictionary<string, FileDeletionEvidence> clientDeleted = local.GroupBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.OrderByDescending(file => file.ChangeNumber).First()).Where(file => file.Deleted)
                        .ToDictionary(file => file.RelativePath, file => new FileDeletionEvidence(file.RelativePath, file.PreviousFile), StringComparer.OrdinalIgnoreCase);
                    rows = job.Changes?.Select(change =>
                    {
                        string path = change.Current?.RelativePath ?? change.Previous?.RelativePath ?? "";
                        clientDeleted.TryGetValue(path, out FileDeletionEvidence? clientDeletion);
                        serverDeleted.TryGetValue(path, out FileDeletionEvidence? serverDeletion);
                        bool deletedOnClient = change.Previous is null && change.Current is not null && clientDeletion is not null;
                        bool deletedOnServer = change.Current is null && change.Previous is not null && serverDeletion is not null;
                        FileDeletionEvidence? evidence = deletedOnClient ? clientDeletion : deletedOnServer ? serverDeletion : null;
                        return ChangeRowMapper.FromDto(change) with
                        {
                            IsConflict = change.ChangeType == FileChangeKind.Modified || deletedOnClient || deletedOnServer,
                            ConflictKind = deletedOnClient ? FileConflictKind.DeletedOnClient
                                : deletedOnServer ? FileConflictKind.DeletedOnServer : FileConflictKind.Content,
                            DeletedVersionSize = evidence?.PreviousFile?.Size,
                            DeletedVersionModifiedUtc = evidence?.PreviousFile?.LastWriteTimeUtc,
                            PreviousModifiedUtc = change.Previous?.LastWriteTimeUtc,
                            CurrentModifiedUtc = change.Current?.LastWriteTimeUtc,
                            Action = _decisions.Decide(change.Previous is null ? null : ToSnapshot(change.Previous),
                                change.Current is null ? null : ToSnapshot(change.Current), clientDeletion, serverDeletion,
                                SelectedProfile?.Rules ?? new ConflictRules())
                        };
                    }).ToArray() ?? [];
                    Status = "Сравнение завершено";
                }

                _completedComparison = job.Status == ScanJobState.Completed ? job : null;
                _activeJob = null;
                Changes = rows;                Error = job.Error ?? "";
            }

            RefreshCommands();
        }

        /// <summary>
        /// Переносит известные атрибуты API в локальный снимок.
        /// </summary>
        private static FileSnapshot ToSnapshot(FileSnapshotDto file) => new(file.RelativePath, file.Size, file.LastWriteTimeUtc);    }
}
