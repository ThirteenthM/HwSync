namespace HwSync.Abstractions.Models
{
    /// <summary>
    /// Правила разрешения конфликтов двусторонней синхронизации.
    /// </summary>
    public sealed record ReconciliationRules
    {
        public ContentConflictPolicy ContentConflict
        {
            get;
            init;
        } = ContentConflictPolicy.KeepBoth;
        public DeletionConflictPolicy DeletionConflict
        {
            get;
            init;
        } = DeletionConflictPolicy.AskUser;
    }
}
