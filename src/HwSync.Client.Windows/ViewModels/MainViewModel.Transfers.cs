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
        /// <summary>Скачивает только отсутствующие клиентские файлы выбранного сравнения.</summary>
        private async Task CopyMissingAsync()
        {
            ScanJobResponse? comparison = _completedComparison;
            if (comparison is null || _jobClient is not IFileDownloadClient downloader)
            {
                return;
            }

            await ExecuteAsync(async () =>
            {
                _completedComparison = null;
                _cancelRequested = false;
                using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                _copyCancellation = cancellation;
                RefreshCommands();
                try
                {
                    FileSnapshot[] files = comparison.Changes!.Where(change => change.ChangeType == FileChangeKind.Created).Select(change => new FileSnapshot(change.Current!.RelativePath, change.Current.Size, change.Current.LastWriteTimeUtc)).ToArray();
                    Status = $"Копирование отсутствующих файлов: {files.Length}…";
                    MissingFileSynchronizer synchronizer = new();
                    IReadOnlyList<FileCopyResult> results = await synchronizer.CopyAsync(_comparedClientRoot, files, (file, stream, token) => downloader.DownloadFileAsync(comparison.Id, file.RelativePath, stream, token), cancellation.Token);
                    Status = $"Скопировано: {results.Count(result => result.Copied)}. Пропущено или с ошибкой: {results.Count(result => !result.Copied)}. Повторите сравнение.";
                    Error = string.Join("\n", results.Where(result => !result.Copied).Take(3).Select(result => result.RelativePath + ": " + result.Error));
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    Status = "Копирование отменено. Уже полученные файлы сохранены. Повторите сравнение.";
                }
                finally
                {
                    _copyCancellation = null;
                    RefreshCommands();
                }
            });
        }

        /// <summary>Выполняет выбранное файловое действие с подтверждением удаления.</summary>
        private async Task ApplyManualAsync(ManualFileOperation operation)
        {
            ScanJobResponse? comparison = _completedComparison;
            if (comparison is null || _jobClient is not IFileMutationClient remote)
            {
                return;
            }

            FileChangeKind kind = operation == ManualFileOperation.DeleteServer ? FileChangeKind.Created : FileChangeKind.Deleted;
            FileSnapshot[] files = comparison.Changes!.Where(change => change.ChangeType == kind).Select(change => kind == FileChangeKind.Created ? change.Current! : change.Previous!).Select(file => new FileSnapshot(file.RelativePath, file.Size, file.LastWriteTimeUtc)).ToArray();
            if (files.Length == 0)
            {
                Status = "Для выбранного действия нет файлов.";
                return;
            }

            if (operation != ManualFileOperation.Upload && ConfirmDeletion?.Invoke(operation == ManualFileOperation.DeleteServer ? "на сервере" : "на клиенте", files.Select(file => file.RelativePath).ToArray()) != true)
            {
                return;
            }

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
                        if (operation == ManualFileOperation.Upload)
                        {
                            await using Stream source = reader.OpenRead(_comparedClientRoot, file);
                            await remote.UploadFileAsync(comparison.Id, file.RelativePath, source, cancellation.Token);
                        }
                        else if (operation == ManualFileOperation.DeleteServer)
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
                    Error = exception is OperationCanceledException ? "Уже выполненные действия сохранены. Результат последнего запроса проверьте сравнением." : DescribeError(exception);
                }
                finally
                {
                    _copyCancellation = null;
                    RefreshCommands();
                }
            });
        }
    }
}
