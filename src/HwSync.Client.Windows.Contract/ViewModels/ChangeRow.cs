namespace HwSync.Client.Windows.Contract.ViewModels
{
    /// <summary>
    /// Строка различий для отображения в таблице клиента.
    /// </summary>
    public sealed record ChangeRow(string Kind, string Path, long? PreviousSize, long? CurrentSize)
    {
        public FileSyncDecision Action { get; init; } = FileSyncDecision.AskUser;

        public string ActionIcon => Action switch
        {
            FileSyncDecision.CopyToServer => "↑",
            FileSyncDecision.CopyToClient => "↓",
            FileSyncDecision.DeleteOnServer => "× ↑",
            FileSyncDecision.DeleteOnClient => "× ↓",
            FileSyncDecision.Skip => "—",
            _ => "?"
        };

        public string ActionDescription => Action switch
        {
            FileSyncDecision.CopyToServer => "Копировать на сервер",
            FileSyncDecision.CopyToClient => "Копировать на клиент",
            FileSyncDecision.DeleteOnServer => "Удалить на сервере",
            FileSyncDecision.DeleteOnClient => "Удалить на клиенте",
            FileSyncDecision.Skip => "Оставить без изменений по правилу профиля",
            _ => "Требуется решение пользователя; автосинхронизация пропустит файл"
        };

        /// <summary>
        /// Размер файла клиента в мегабайтах.
        /// </summary>
        public decimal? PreviousSizeMegabytes => PreviousSize / 1_000_000m;

        /// <summary>
        /// Размер файла сервера в мегабайтах.
        /// </summary>
        public decimal? CurrentSizeMegabytes => CurrentSize / 1_000_000m;
    }
}
