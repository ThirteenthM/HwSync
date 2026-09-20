namespace HwSync.Windows.Contract.Client.Configuration
{
    /// <summary>
    /// Правила текущего режима копирования отсутствующих файлов.
    /// </summary>
    public sealed class ConflictRules
    {
        public SyncRule MissingOnClient { get; init; } = SyncRule.Copy;

        public SyncRule DifferentFiles { get; init; } = SyncRule.KeepBoth;

        public SyncRule ClientOnlyFiles { get; init; } = SyncRule.Keep;

        /// <summary>
        /// Правило для неизменившейся клиентской копии после удаления на сервере.
        /// </summary>
        public SyncRule ServerDeletions { get; init; } = SyncRule.Delete;

        /// <summary>
        /// Правило для неизменившейся серверной копии после удаления на клиенте.
        /// </summary>
        public SyncRule ClientDeletions { get; init; } = SyncRule.Delete;
    }
}
