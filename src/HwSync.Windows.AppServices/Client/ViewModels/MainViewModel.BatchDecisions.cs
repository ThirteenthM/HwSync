using CommunityToolkit.Mvvm.Input;
using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.AppServices.Client.ViewModels
{
    /// <summary>
    /// Применение общего решения к выбранным файлам без выполнения операций.
    /// </summary>
    public sealed partial class MainViewModel
    {
        public IRelayCommand<BatchDecisionChoice> SetBatchDecisionCommand { get; }

        System.Windows.Input.ICommand IMainViewModel.SetBatchDecisionCommand => SetBatchDecisionCommand;

        /// <summary>
        /// Обновляет применимые строки, сохраняя остальные решения.
        /// </summary>
        private void SetBatchDecision(BatchDecisionChoice? choice)
        {
            if (!CanEditPlan || choice is null || choice.Paths.Count == 0)
            {
                return;
            }

            HashSet<string> selected = new(choice.Paths, StringComparer.Ordinal);
            HashSet<string> visible = new(VisibleChanges.Select(row => row.Path), StringComparer.Ordinal);
            HashSet<string> duplicates = new(Changes.GroupBy(row => row.Path, StringComparer.Ordinal)
                .Where(group => group.Count() > 1).Select(group => group.Key), StringComparer.Ordinal);
            int updated = 0;
            Changes = Changes.Select(row =>
            {
                if (!selected.Contains(row.Path) || !visible.Contains(row.Path) || duplicates.Contains(row.Path))
                {
                    return row;
                }

                FileSyncDecision action = choice.Action;
                if (action == FileSyncDecision.CopyToClient && row.PreviousSize.HasValue)
                {
                    action = FileSyncDecision.ReplaceOnClient;
                }
                else if (action == FileSyncDecision.CopyToServer && row.CurrentSize.HasValue)
                {
                    action = FileSyncDecision.ReplaceOnServer;
                }

                if (!row.ActionChoices.Any(item => item.Action == action))
                {
                    return row;
                }

                updated++;
                return row with { Action = action, IsManualDecision = true };
            }).ToArray();
            Error = "";
            Status = $"Обновлено: {updated}. Неприменимо: {selected.Count - updated}. Изменён только план.";
            RefreshCommands();
        }
    }
}