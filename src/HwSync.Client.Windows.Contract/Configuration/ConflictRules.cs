namespace HwSync.Client.Windows.Contract.Configuration
{
    /// <summary>
    /// Правила текущего режима копирования отсутствующих файлов.
    /// </summary>
    public sealed class ConflictRules
    {
        public SyncRule MissingOnClient { get; init; } = SyncRule.Copy;

        public SyncRule DifferentFiles { get; init; } = SyncRule.AskUser;

        public SyncRule ClientOnlyFiles { get; init; } = SyncRule.Keep;

        /// <summary>
        /// Резерв для истории удалений на сервере; пока не влияет на автосинхронизацию.
        /// </summary>
        public SyncRule ServerDeletions { get; init; } = SyncRule.RecordOnly;
    }
}
