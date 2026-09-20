using CommunityToolkit.Mvvm.Input;
using HwSync.Abstractions.Models;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.AppServices.Client.ViewModels
{
    /// <summary>
    /// Выбор и выполнение решений для различающихся версий файла.
    /// </summary>
    public sealed partial class MainViewModel
    {
        public Func<ChangeRow, FileSyncDecision?>? ChooseConflictResolution { get; set; }

        public IRelayCommand<ChangeRow> ResolveConflictCommand { get; }

        System.Windows.Input.ICommand IMainViewModel.ResolveConflictCommand => ResolveConflictCommand;

        /// <summary>
        /// Записывает выбор пользователя в план без изменения файлов.
        /// </summary>
        private void ResolveConflict(ChangeRow? row)
        {
            if (row is null || !ResolveConflictCommand.CanExecute(row))
            {
                return;
            }

            if (ChooseConflictResolution is null)
            {
                Error = "Окно разбора конфликтов не подключено.";
                return;
            }

            FileSyncDecision? decision = ChooseConflictResolution(row);
            if (decision is null)
            {
                return;
            }

            bool allowed = decision is FileSyncDecision.Skip or FileSyncDecision.AskUser
                || row.ConflictKind switch
                {
                    FileConflictKind.DeletedOnServer => decision is FileSyncDecision.CopyToServer or FileSyncDecision.DeleteOnClient,
                    FileConflictKind.DeletedOnClient => decision is FileSyncDecision.CopyToClient or FileSyncDecision.DeleteOnServer,
                    _ => decision is FileSyncDecision.ReplaceOnClient or FileSyncDecision.ReplaceOnServer or FileSyncDecision.KeepBoth
                };
            if (!allowed)
            {
                Error = "Выбранное действие не поддерживается для этого конфликта.";
                return;
            }
            Changes = Changes.Select(item => item == row ? item with { Action = decision.Value } : item).ToArray();
            Error = "";
            Status = "Решение добавлено в план. Для выполнения нажмите «Автосинхронизация».";
            RefreshCommands();
        }

        /// <summary>
        /// Выполняет выбранное решение с сохранением оригиналов до завершения передачи.
        /// </summary>
        private async Task ApplyConflictAsync(Guid comparisonId, FileChangeDto change, FileSyncDecision decision, CancellationToken token)
        {
            FileSnapshotDto previous = change.Previous!;
            FileSnapshotDto current = change.Current!;
            FileSnapshot client = new(previous.RelativePath, previous.Size, previous.LastWriteTimeUtc);
            FileSnapshot server = new(current.RelativePath, current.Size, current.LastWriteTimeUtc);
            if (decision == FileSyncDecision.ReplaceOnServer)
            {
                await MeasureTransferAsync(client, async () =>
                {
                    await using Stream source = _fileReader.OpenRead(_comparedClientRoot, client);
                    await ((IConflictFileClient)_jobClient!).ReplaceServerFileAsync(comparisonId, client.RelativePath, source, token);
                    return true;
                }, copied => copied);
                return;
            }

            if (decision == FileSyncDecision.KeepBoth)
            {
                // Обе резервные копии создаются до замены клиентского оригинала.
                FileSnapshot backup = new(ConflictCopyPath.Create(client.RelativePath, comparisonId), client.Size, client.LastWriteTimeUtc);
                await using (Stream source = _fileReader.OpenRead(_comparedClientRoot, client))
                {
                    await _fileOperations.CopyMissingAsync(_comparedClientRoot, backup, source, token);
                }

                await MeasureTransferAsync(client, async () =>
                {
                    await using Stream source = _fileReader.OpenRead(_comparedClientRoot, client);
                    await ((IConflictFileClient)_jobClient!).PreserveClientFileAsync(comparisonId, client.RelativePath, source, token);
                    return true;
                }, copied => copied);
            }

            await MeasureTransferAsync(server, async () =>
            {
                await _conflictFiles.ReplaceAsync(_comparedClientRoot, client, server,
                    (output, cancellation) => ((IFileDownloadClient)_jobClient!).DownloadFileAsync(
                        comparisonId, server.RelativePath, output, cancellation), token);
                return true;
            }, copied => copied);
        }
    }
}
