using System.IO;
using HwSync.Abstractions.Models;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Client.Windows.Contract.ViewModels;

namespace HwSync.Client.Windows.Application.ViewModels
{
    /// <summary>
    /// Выполнение решений, показанных в таблице сравнения.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>
        /// Проверяет наличие свежего плана и поддержку нужных операций.
        /// </summary>
        private bool CanAutoSync() => !_busy && _completedComparison is not null
            && Changes.Any(row => IsExecutable(row.Action))
            && Changes.All(row => row.Action switch
            {
                FileSyncDecision.CopyToClient => _jobClient is IFileDownloadClient,
                FileSyncDecision.CopyToServer or FileSyncDecision.DeleteOnClient or FileSyncDecision.DeleteOnServer => _jobClient is IFileMutationClient,
                _ => true
            });

        /// <summary>
        /// Отличает файловую операцию от пропуска и нерешённого конфликта.
        /// </summary>
        private static bool IsExecutable(FileSyncDecision action) => action is FileSyncDecision.CopyToClient
            or FileSyncDecision.CopyToServer or FileSyncDecision.DeleteOnClient or FileSyncDecision.DeleteOnServer;

        /// <summary>
        /// Выполняет показанный план, пропуская вопросы и оставленные файлы.
        /// </summary>
        private async Task AutoSyncAsync()
        {
            ScanJobResponse? comparison = _completedComparison;
            if (!CanAutoSync() || comparison?.Changes is null)
            {
                return;
            }

            (FileChangeDto Change, FileSyncDecision Action)[] plan = comparison.Changes
                .Zip(Changes, (change, row) => (Change: change, Action: row.Action))
                .Where(item => IsExecutable(item.Action)).ToArray();
            int unresolved = Changes.Count(row => row.Action == FileSyncDecision.AskUser);
            int skipped = Changes.Count(row => row.Action == FileSyncDecision.Skip);
            foreach (FileSyncDecision deletion in new[] { FileSyncDecision.DeleteOnServer, FileSyncDecision.DeleteOnClient })
            {
                string[] paths = plan.Where(item => item.Action == deletion)
                    .Select(item => (item.Change.Current ?? item.Change.Previous)!.RelativePath).ToArray();
                if (paths.Length > 0 && ConfirmDeletion?.Invoke(
                    deletion == FileSyncDecision.DeleteOnServer ? "на сервере" : "на клиенте", paths) != true)
                {
                    return;
                }
            }

            await ExecuteAsync(async () =>
            {
                _completedComparison = null;
                _cancelRequested = false;
                using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                _copyCancellation = cancellation;
                RefreshCommands();
                BeginTransferMetrics("Автосинхронизация", plan.Any(item =>
                    item.Action is FileSyncDecision.CopyToClient or FileSyncDecision.CopyToServer));
                int completed = 0;
                try
                {
                    foreach ((FileChangeDto change, FileSyncDecision action) in plan)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        FileSnapshotDto snapshot = (change.Current ?? change.Previous)!;
                        FileSnapshot file = new(snapshot.RelativePath, snapshot.Size, snapshot.LastWriteTimeUtc);
                        Status = $"Автосинхронизация {completed + 1} из {plan.Length}: {file.RelativePath}";
                        await ApplyPlannedActionAsync(comparison.Id, file, action, cancellation.Token);
                        completed++;
                    }

                    Status = $"Выполнено: {completed}. Требуют решения: {unresolved}. Оставлено по правилам: {skipped}. Повторите сравнение.";
                }
                catch (Exception exception)
                {
                    Status = $"Автосинхронизация остановлена. Выполнено: {completed} из {plan.Length}. Повторите сравнение.";
                    Error = exception is OperationCanceledException
                        ? "Уже выполненные действия сохранены. Результат последнего запроса проверьте сравнением."
                        : DescribeError(exception);
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
        /// Выполняет одно решение с проверками актуальности файлов.
        /// </summary>
        private async Task ApplyPlannedActionAsync(Guid comparisonId, FileSnapshot file, FileSyncDecision action, CancellationToken token)
        {
            switch (action)
            {
                case FileSyncDecision.CopyToClient:
                    IFileDownloadClient downloader = (IFileDownloadClient)_jobClient!;
                    IReadOnlyList<FileCopyResult> results = await MeasureTransferAsync(file,
                        () => _synchronizer.CopyAsync(_comparedClientRoot, [file],
                            (snapshot, stream, cancellation) => downloader.DownloadFileAsync(
                                comparisonId, snapshot.RelativePath, stream, cancellation), token),
                        copied => copied.Single().Copied);
                    if (!results.Single().Copied)
                    {
                        throw new IOException($"{file.RelativePath}: {results.Single().Error}");
                    }

                    break;
                case FileSyncDecision.CopyToServer:
                    await MeasureTransferAsync(file, async () =>
                    {
                        await using Stream source = _fileReader.OpenRead(_comparedClientRoot, file);
                        await ((IFileMutationClient)_jobClient!).UploadFileAsync(comparisonId, file.RelativePath, source, token);
                        return true;
                    }, copied => copied);
                    break;
                case FileSyncDecision.DeleteOnServer:
                    _fileOperations.EnsureMissing(_comparedClientRoot, file.RelativePath);
                    await ((IFileMutationClient)_jobClient!).DeleteServerFileAsync(comparisonId, file.RelativePath, token);
                    break;
                case FileSyncDecision.DeleteOnClient:
                    await ((IFileMutationClient)_jobClient!).EnsureServerFileMissingAsync(comparisonId, file.RelativePath, token);
                    token.ThrowIfCancellationRequested();
                    _fileOperations.DeleteUnchanged(_comparedClientRoot, file);
                    break;
            }
        }
    }
}