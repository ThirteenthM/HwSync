using HwSync.Client.Windows.Contract.ViewModels;
using HwSync.Client.Windows.Contract.Configuration;
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

namespace HwSync.Client.Windows.Application.ViewModels
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
                ApplyJob(job);
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
                    ApplyJob(job);
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
                Changes = job.Changes?.Select(change => ChangeRowMapper.FromDto(change) with
                {
                    Action = _decisions.Decide(change.Previous is not null, change.Current is not null,
                        SelectedProfile?.Rules ?? new ConflictRules())
                }).ToArray() ?? [];
                Error = job.Error ?? "";
            }

            RefreshCommands();
        }
    }
}
