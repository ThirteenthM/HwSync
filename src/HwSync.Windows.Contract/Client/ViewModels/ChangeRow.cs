namespace HwSync.Windows.Contract.Client.ViewModels
{
    /// <summary>
    /// Строка различий для отображения в таблице клиента.
    /// </summary>
    public sealed record ChangeRow(string Kind, string Path, long? PreviousSize, long? CurrentSize)
    {
        public DateTime? PreviousModifiedUtc { get; init; }

        public DateTime? CurrentModifiedUtc { get; init; }

        public bool IsConflict { get; init; }

        public FileConflictKind ConflictKind { get; init; }

        public bool IsDeletionConflict => ConflictKind is FileConflictKind.DeletedOnClient or FileConflictKind.DeletedOnServer;

        public long? DeletedVersionSize { get; init; }

        public DateTime? DeletedVersionModifiedUtc { get; init; }

        public string ConflictDescription => IsDeletionConflict
            ? $"Файл удалён {(ConflictKind == FileConflictKind.DeletedOnServer ? "на сервере" : "на клиенте")}. " +
                (DeletedVersionSize is long size && DeletedVersionModifiedUtc is DateTime modified
                    ? $"Последняя известная версия: {size / 1_000_000m:0.000} МБ, {modified:yyyy-MM-dd HH:mm:ss} UTC."
                    : "Последняя известная версия недоступна; автоматическое удаление запрещено.")
            : "Файлы существуют на обеих сторонах и отличаются.";

        public FileSyncDecision Action { get; init; } = FileSyncDecision.AskUser;

        public string ActionIcon => Action switch
        {
            FileSyncDecision.CopyToServer => "↑",
            FileSyncDecision.CopyToClient => "↓",
            FileSyncDecision.DeleteOnServer => "× ↑",
            FileSyncDecision.DeleteOnClient => "× ↓",
            FileSyncDecision.ReplaceOnClient => "⇓",
            FileSyncDecision.ReplaceOnServer => "⇑",
            FileSyncDecision.KeepBoth => "⇄",
            FileSyncDecision.Skip => "—",
            _ => "?"
        };

        public string ActionDescription => Action switch
        {
            FileSyncDecision.CopyToServer => "Копировать на сервер",
            FileSyncDecision.CopyToClient => "Копировать на клиент",
            FileSyncDecision.DeleteOnServer => "Удалить на сервере",
            FileSyncDecision.DeleteOnClient => "Удалить на клиенте",
            FileSyncDecision.ReplaceOnClient => "Заменить версию клиента серверной",
            FileSyncDecision.ReplaceOnServer => "Заменить версию сервера клиентской",
            FileSyncDecision.KeepBoth => "Сохранить обе версии на обеих сторонах",
            FileSyncDecision.Skip => "Оставить без изменений",
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
