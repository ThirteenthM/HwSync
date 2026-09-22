using System.IO;
using HwSync.Abstractions.Models;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using CommunityToolkit.Mvvm.Input;
using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.AppServices.Client.ViewModels
{
    /// <summary>
    /// Редактирование плана по относительным путям без изменения файлов.
    /// </summary>
    public sealed partial class MainViewModel
    {
        public bool CanEditPlan => !_busy && _completedComparison is not null;

        public IRelayCommand<FileDecisionChoice> SetFileDecisionCommand { get; }

        System.Windows.Input.ICommand IMainViewModel.SetFileDecisionCommand => SetFileDecisionCommand;

        /// <summary>
        /// Проверяет обе версии перед удалением выбранной стороны.
        /// </summary>
        private async Task DeleteComparedVersionAsync(Guid comparisonId, FileChangeDto change,
            FileSyncDecision decision, CancellationToken token)
        {
            FileSnapshotDto previous = change.Previous!;
            FileSnapshot local = new(previous.RelativePath, previous.Size, previous.LastWriteTimeUtc);
            IComparedFileMutationClient api = (IComparedFileMutationClient)_jobClient!;
            using (Stream source = _fileReader.OpenRead(_comparedClientRoot, local))
            {
                token.ThrowIfCancellationRequested();
            }

            if (decision == FileSyncDecision.DeleteOnServer)
            {
                await api.DeleteComparedServerFileAsync(comparisonId, local.RelativePath, token);
            }
            else
            {
                await api.EnsureServerFileUnchangedAsync(comparisonId, local.RelativePath, token);
                token.ThrowIfCancellationRequested();
                _fileOperations.DeleteUnchanged(_comparedClientRoot, local);
            }
        }

        /// <summary>
        /// Проверяет свежесть плана, однозначность пути и применимость действия.
        /// </summary>
        private bool CanSetFileDecision(FileDecisionChoice? choice)
        {
            if (!CanEditPlan || choice is null)
            {
                return false;
            }

            ChangeRow[] matching = Changes.Where(row => row.Path == choice.Path).ToArray();
            return matching.Length == 1 && matching[0].ActionChoices.Any(item => item.Action == choice.Action);
        }

        /// <summary>
        /// Сохраняет ручной выбор только для выбранного пути текущего плана.
        /// </summary>
        private void SetFileDecision(FileDecisionChoice? choice)
        {
            if (!CanSetFileDecision(choice))
            {
                return;
            }

            Changes = Changes.Select(row => row.Path == choice!.Path
                ? row with { Action = choice.Action, IsManualDecision = true }
                : row).ToArray();
            Error = "";
            Status = "Решение добавлено в план. Для выполнения нажмите «Синхронизировать».";
            RefreshCommands();
        }
    }
}
