namespace HwSync.Windows.Contract.Client.ViewModels
{
    /// <summary>
    /// Действие для выбранных относительных путей текущего плана.
    /// </summary>
    public sealed record BatchDecisionChoice(IReadOnlyList<string> Paths, FileSyncDecision Action);
}