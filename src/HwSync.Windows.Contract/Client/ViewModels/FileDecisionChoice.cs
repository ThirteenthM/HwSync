namespace HwSync.Windows.Contract.Client.ViewModels
{
    /// <summary>
    /// Действие пользователя для конкретного относительного пути.
    /// </summary>
    public sealed record FileDecisionChoice(string Path, FileSyncDecision Action, string Description);
}
