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

        public bool IsUnchanged { get; init; }

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

        public bool IsManualDecision { get; init; }

        public string RelativeFolder => NormalizedPath.Contains('/') ? NormalizedPath[..NormalizedPath.LastIndexOf('/')] : "";

        public string FileName => NormalizedPath[(NormalizedPath.LastIndexOf('/') + 1)..];

        private string NormalizedPath => Path.Replace('\\', '/');

        public string? ServerPath => CurrentSize.HasValue ? FileName : null;

        public string? ClientPath => PreviousSize.HasValue ? FileName : null;

        public string DecisionSource => IsManualDecision ? "Выбрано пользователем" : "Предложено стратегией";

        public IReadOnlyList<FileDecisionChoice> ActionChoices => CreateActionChoices();

        /// <summary>
        /// Предлагает только действия, для которых существует исходный файл.
        /// </summary>
        private IReadOnlyList<FileDecisionChoice> CreateActionChoices()
        {
            List<FileDecisionChoice> choices = [];
            if (CurrentSize.HasValue && !IsUnchanged)
            {
                choices.Add(new(Path, PreviousSize.HasValue ? FileSyncDecision.ReplaceOnClient : FileSyncDecision.CopyToClient,
                    PreviousSize.HasValue ? "→ На клиент (заменить)" : "→ На клиент"));
                choices.Add(new(Path, FileSyncDecision.DeleteOnServer, "× Слева: удалить на сервере"));
            }

            if (PreviousSize.HasValue && !IsUnchanged)
            {
                choices.Add(new(Path, CurrentSize.HasValue ? FileSyncDecision.ReplaceOnServer : FileSyncDecision.CopyToServer,
                    CurrentSize.HasValue ? "← На сервер (заменить)" : "← На сервер"));
                choices.Add(new(Path, FileSyncDecision.DeleteOnClient, "× Справа: удалить на клиенте"));
            }

            if (IsUnchanged)
            {
                choices.Add(new(Path, FileSyncDecision.DeleteOnServer, "× Слева: удалить на сервере"));
                choices.Add(new(Path, FileSyncDecision.DeleteOnClient, "× Справа: удалить на клиенте"));
            }

            if (PreviousSize.HasValue && CurrentSize.HasValue && !IsUnchanged)
            {
                choices.Add(new(Path, FileSyncDecision.KeepBoth, "⇄ Сохранить обе версии"));
            }

            if (PreviousSize.HasValue || CurrentSize.HasValue)
            {
                choices.Add(new(Path, FileSyncDecision.DeleteBoth, "× Удалить на обеих сторонах"));
            }

            choices.Add(new(Path, FileSyncDecision.Skip, "= Ничего не делать"));
            choices.Add(new(Path, FileSyncDecision.AskUser, "? Решить позже"));
            return choices;
        }

        public string ActionIcon => Action switch
        {
            FileSyncDecision.CopyToServer => "←",
            FileSyncDecision.CopyToClient => "→",
            FileSyncDecision.DeleteOnServer => "× слева",
            FileSyncDecision.DeleteOnClient => "× справа",
            FileSyncDecision.ReplaceOnClient => "→",
            FileSyncDecision.ReplaceOnServer => "←",
            FileSyncDecision.KeepBoth => "⇄",
            FileSyncDecision.DeleteBoth => "× обе",
            FileSyncDecision.Skip => "=",
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
            FileSyncDecision.DeleteBoth => "Удалить на обеих сторонах",
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
