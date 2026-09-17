using HwSync.Abstractions.Models;
using HwSync.Abstractions.Services;

namespace HwSync.Core.Services
{
    /// <summary>Преобразование различий в односторонний план.</summary>
    public sealed class SyncPlanner : ISyncPlanner
    {
        /// <summary>Назначает действия согласно выбранному режиму синхронизации.</summary>
        public SyncPlan Create(IReadOnlyCollection<FileChange> changes, SyncMode mode)
        {
            ArgumentNullException.ThrowIfNull(changes);
            if (!Enum.IsDefined(mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }
            List<SyncPlanItem> items = new();
            HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
            foreach (FileChange change in changes)
            {
                string path = change.Current?.RelativePath ?? change.Previous?.RelativePath
                    ?? throw new ArgumentException("У изменения отсутствует путь файла.");
                string normalized = path.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith('/') || normalized.Contains(':')
                    || normalized.Split('/').Any(part => part is "" or "." or "..") || !paths.Add(normalized))
                {
                    throw new ArgumentException("План содержит недопустимый или повторяющийся относительный путь.");
                }
                SyncAction action = change.ChangeType switch
                {
                    FileChangeType.Created when change.Previous is null && change.Current is not null => SyncAction.CopyToClient,
                    FileChangeType.Modified when change.Previous is not null && change.Current is not null
                        && change.Previous.RelativePath == change.Current.RelativePath => mode == SyncMode.CopyMissing ? SyncAction.KeepOnClient : SyncAction.ReplaceOnClient,
                    FileChangeType.Deleted when change.Previous is not null && change.Current is null =>
                        mode == SyncMode.Mirror ? SyncAction.DeleteFromClient : SyncAction.KeepOnClient,
                    _ => throw new ArgumentException("Некорректное изменение для плана синхронизации.")
                };
                items.Add(new(action, path, change.Previous, change.Current));
            }
            return new(mode, items.AsReadOnly());
        }
    }
}
