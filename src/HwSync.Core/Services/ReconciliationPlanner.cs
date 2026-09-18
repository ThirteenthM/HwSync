using HwSync.Abstractions.Models;
using HwSync.Abstractions.Services;

namespace HwSync.Core.Services
{
    /// <summary>
    /// Строит план по полным снимкам, не изменяя файлы и подтверждённые версии.
    /// </summary>
    public sealed class ReconciliationPlanner : IReconciliationPlanner
    {
        /// <summary>
        /// Строит решения по общему состоянию, снимкам сторон и правилам конфликтов.
        /// </summary>
        public ReconciliationPlan Create(FolderSyncState baseline, IReadOnlyCollection<FileVersion> server,
            IReadOnlyCollection<FileVersion> client, ReconciliationRules rules)
        {
            ArgumentNullException.ThrowIfNull(baseline);
            ArgumentNullException.ThrowIfNull(rules);
            if (baseline.ClientId == Guid.Empty || string.IsNullOrWhiteSpace(baseline.FolderId))
            {
                throw new ArgumentException("Требуются идентификаторы клиента и папки.");
            }
            if (!Enum.IsDefined(rules.ContentConflict) || !Enum.IsDefined(rules.DeletionConflict))
            {
                throw new ArgumentException("Неизвестная стратегия конфликта.");
            }

            Dictionary<string, FileVersion> previous = Index(baseline.Files);
            Dictionary<string, FileVersion> remote = Index(server);
            Dictionary<string, FileVersion> local = Index(client);
            List<ReconciliationItem> items = new();
            foreach (string path in previous.Keys.Union(remote.Keys, StringComparer.OrdinalIgnoreCase)
                .Union(local.Keys, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
            {
                previous.TryGetValue(path, out FileVersion? before);
                remote.TryGetValue(path, out FileVersion? onServer);
                local.TryGetValue(path, out FileVersion? onClient);
                if (before is not null && onServer is not null && onServer.Revision < before.Revision)
                {
                    throw new ArgumentException("Версия сервера старше подтверждённой версии клиента.");
                }
                items.Add(new(path, Decide(before, onServer, onClient), before, onServer, onClient));
            }
            return new(items.AsReadOnly());
        }

        /// <summary>
        /// Выбирает действие по изменениям относительно общего состояния.
        /// </summary>
        private static ReconciliationAction Decide(FileVersion? before, FileVersion? server, FileVersion? client)
        {
            string? remoteHash = server?.ContentHash;
            string? localHash = client?.ContentHash;
            if (Same(remoteHash, localHash))
            {
                return ReconciliationAction.None;
            }

            // A new client has no evidence that its absence means deletion.
            if (before is null)
            {
                if (remoteHash is not null && localHash is not null)
                {
                    return ReconciliationAction.KeepBoth;
                }
                if (server is not null && remoteHash is null && localHash is not null)
                {
                    return ReconciliationAction.NeedsDecision;
                }
                if (client is not null && localHash is null && remoteHash is not null)
                {
                    return ReconciliationAction.NeedsDecision;
                }
                return remoteHash is null ? ReconciliationAction.Upload : ReconciliationAction.Download;
            }

            bool remoteChanged = !Same(remoteHash, before.ContentHash);
            bool localChanged = !Same(localHash, before.ContentHash);
            if (remoteChanged && localChanged)
            {
                return remoteHash is null || localHash is null
                    ? ReconciliationAction.NeedsDecision : ReconciliationAction.KeepBoth;
            }
            if (remoteChanged)
            {
                return remoteHash is null ? ReconciliationAction.DeleteOnClient : ReconciliationAction.Download;
            }
            return localHash is null ? ReconciliationAction.DeleteOnServer : ReconciliationAction.Upload;
        }

        /// <summary>
        /// Сравнивает хеши содержимого без учёта регистра.
        /// </summary>
        private static bool Same(string? first, string? second) =>
            StringComparer.OrdinalIgnoreCase.Equals(first, second);

        /// <summary>
        /// Проверяет версии и индексирует их по нормализованным путям.
        /// </summary>
        private static Dictionary<string, FileVersion> Index(IReadOnlyCollection<FileVersion> files)
        {
            ArgumentNullException.ThrowIfNull(files);
            Dictionary<string, FileVersion> result = new(StringComparer.OrdinalIgnoreCase);
            foreach (FileVersion file in files)
            {
                ArgumentNullException.ThrowIfNull(file);
                string path = file.RelativePath?.Replace('\\', '/') ?? "";
                if (string.IsNullOrWhiteSpace(path) || path.Contains(':')
                    || path.Split('/').Any(part => part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' '))
                    || file.Revision < 0 || file.ContentHash is not null
                    && (file.ContentHash.Length != 64 || !file.ContentHash.All(Uri.IsHexDigit))
                    || !result.TryAdd(path, file with
                    {
                        RelativePath = path
                    }))
                {
                    throw new ArgumentException("Некорректный путь, версия, SHA-256 или повторный файл в снимке.");
                }
            }
            return result;
        }
    }
}
