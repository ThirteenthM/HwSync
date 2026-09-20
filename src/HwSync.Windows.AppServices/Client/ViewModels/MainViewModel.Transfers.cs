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
        /// Скачивает только отсутствующие клиентские файлы выбранного сравнения.
        /// </summary>
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
                BeginTransferMetrics("Сервер → клиент");
                try
                {
                    FileSnapshot[] files = comparison.Changes!.Where(change => change.ChangeType == FileChangeKind.Created).Select(change => new FileSnapshot(change.Current!.RelativePath, change.Current.Size, change.Current.LastWriteTimeUtc)).ToArray();
                    Status = $"Копирование отсутствующих файлов: {files.Length}…";
                    IMissingFileSynchronizer synchronizer = _synchronizer;
                    List<FileCopyResult> results = new();
                    for (int index = 0; index < files.Length; index++)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        FileSnapshot file = files[index];
                        Status = $"Копирование {index + 1} из {files.Length}: {file.RelativePath}";
                        IReadOnlyList<FileCopyResult> fileResults = await MeasureTransferAsync(file,
                            () => synchronizer.CopyAsync(
                                _comparedClientRoot, [file],
                                (snapshot, stream, token) => downloader.DownloadFileAsync(comparison.Id, snapshot.RelativePath, stream, token),
                                cancellation.Token),
                            results => results.Single().Copied);
                        results.AddRange(fileResults);
                    }
                    Status = $"Скопировано: {results.Count(result => result.Copied)}. Пропущено или с ошибкой: {results.Count(result => !result.Copied)}. Повторите сравнение.";
                    Error = string.Join("\n", results.Where(result => !result.Copied).Take(3).Select(result => result.RelativePath + ": " + result.Error));
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    Status = "Копирование отменено. Уже полученные файлы сохранены. Повторите сравнение.";
                }
                finally
                {
                    CompleteTransferMetrics();
                    _copyCancellation = null;
                    RefreshCommands();
                }
            });
        }

        /// <summary>
        /// Выполняет выбранное файловое действие с подтверждением удаления.
        /// </summary>
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
                BeginTransferMetrics("Клиент → сервер", operation == ManualFileOperation.Upload);
                int completed = 0;
                try
                {
                    IComparedFileOperations local = _fileOperations;
                    ISourceFileReader reader = _fileReader;
                    foreach (FileSnapshot file in files)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        Status = $"Обработка {completed + 1} из {files.Length}: {file.RelativePath}";
                        if (operation == ManualFileOperation.Upload)
                        {
                            await MeasureTransferAsync(file, async () =>
                            {
                                await using Stream source = reader.OpenRead(_comparedClientRoot, file);
                                await remote.UploadFileAsync(comparison.Id, file.RelativePath, source, cancellation.Token);
                                return true;
                            }, completed => completed);
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
                    CompleteTransferMetrics();
                    _copyCancellation = null;
                    RefreshCommands();
                }
            });
        }
    }
}
