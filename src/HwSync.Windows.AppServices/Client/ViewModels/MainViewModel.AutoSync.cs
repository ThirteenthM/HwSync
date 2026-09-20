using System.IO;
using HwSync.Abstractions.Models;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.AppServices.Client.ViewModels
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
                FileSyncDecision.CopyToClient or FileSyncDecision.ReplaceOnClient => _jobClient is IFileDownloadClient,
                FileSyncDecision.ReplaceOnServer => _jobClient is IConflictFileClient,
                FileSyncDecision.KeepBoth => _jobClient is IConflictFileClient && _jobClient is IFileDownloadClient,
                FileSyncDecision.CopyToServer or FileSyncDecision.DeleteOnClient or FileSyncDecision.DeleteOnServer => _jobClient is IFileMutationClient,
                _ => true
            });

        /// <summary>
        /// Отличает файловую операцию от пропуска и нерешённого конфликта.
        /// </summary>
        private static bool IsExecutable(FileSyncDecision action) => action is FileSyncDecision.CopyToClient
            or FileSyncDecision.CopyToServer or FileSyncDecision.DeleteOnClient or FileSyncDecision.DeleteOnServer or FileSyncDecision.ReplaceOnClient or FileSyncDecision.ReplaceOnServer or FileSyncDecision.KeepBoth;

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

            string? duplicatePath = Changes.GroupBy(row => row.Path, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Skip(1).Any())?.Key
                ?? comparison.Changes.GroupBy(change => change.Current?.RelativePath ?? change.Previous?.RelativePath ?? "", StringComparer.Ordinal)
                    .FirstOrDefault(group => group.Skip(1).Any())?.Key;
            if (duplicatePath is not null)
            {
                _completedComparison = null;
                Status = "План неоднозначен. Повторите сравнение.";
                Error = $"Путь повторяется в плане: {duplicatePath}. Файлы не изменены.";
                RefreshCommands();
                return;
            }

            Dictionary<string, FileSyncDecision> decisionsByPath = Changes.ToDictionary(
                row => row.Path, row => row.Action, StringComparer.Ordinal);
            (FileChangeDto Change, FileSyncDecision Action)[] plan = comparison.Changes
                .Select(change => (Change: change, Action: decisionsByPath.GetValueOrDefault(
                    change.Current?.RelativePath ?? change.Previous?.RelativePath ?? "",
                    FileSyncDecision.AskUser)))
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
                    item.Action is FileSyncDecision.CopyToClient or FileSyncDecision.CopyToServer or FileSyncDecision.ReplaceOnClient or FileSyncDecision.ReplaceOnServer or FileSyncDecision.KeepBoth));
                int completed = 0;
                int alreadyExists = 0;
                try
                {
                    foreach ((FileChangeDto change, FileSyncDecision action) in plan)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        FileSnapshotDto snapshot = (change.Current ?? change.Previous)!;
                        FileSnapshot file = new(snapshot.RelativePath, snapshot.Size, snapshot.LastWriteTimeUtc);
                        Status = $"Автосинхронизация {completed + alreadyExists + 1} из {plan.Length}: {file.RelativePath}";
                        if (action is FileSyncDecision.ReplaceOnClient or FileSyncDecision.ReplaceOnServer or FileSyncDecision.KeepBoth)
                        {
                            await ApplyConflictAsync(comparison.Id, change, action, cancellation.Token);
                            completed++;
                        }
                        else if (await ApplyPlannedActionAsync(comparison.Id, file, action, cancellation.Token))
                        {
                            completed++;
                        }
                        else
                        {
                            alreadyExists++;
                        }
                    }

                    Status = $"Выполнено: {completed}. Уже существуют: {alreadyExists}. Требуют решения: {unresolved}. Оставлено по правилам: {skipped}. Повторите сравнение.";
                }
                catch (Exception exception)
                {
                    Status = $"Автосинхронизация остановлена. Выполнено: {completed} из {plan.Length}. Уже существуют: {alreadyExists}. Повторите сравнение.";
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
        private async Task<bool> ApplyPlannedActionAsync(Guid comparisonId, FileSnapshot file, FileSyncDecision action, CancellationToken token)
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
                    if (results.Single().AlreadyExists)
                    {
                        return false;
                    }

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
                default:
                    return false;
            }

            return true;
        }
    }
}
