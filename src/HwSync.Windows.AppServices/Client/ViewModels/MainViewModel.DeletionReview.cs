using System.IO;
using HwSync.Abstractions.Models;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.AppServices.Client.ViewModels
{
    /// <summary>
    /// Проверка выбранных копий перед удалением и загрузка просмотра.
    /// </summary>
    public sealed partial class MainViewModel
    {
        public Func<IReadOnlyList<DeletionCandidate>, IReadOnlyList<DeletionCandidate>?>? ReviewDeletions { get; set; }

        /// <summary>
        /// Собирает копии и применяет подтверждённый выбор к плану.
        /// </summary>
        private (FileChangeDto Change, FileSyncDecision Action)[]? ReviewDeletionPlan(
            (FileChangeDto Change, FileSyncDecision Action)[] plan)
        {
            List<DeletionCandidate> candidates = [];
            foreach ((FileChangeDto change, FileSyncDecision action) in plan)
            {
                if (action is FileSyncDecision.DeleteOnServer or FileSyncDecision.DeleteBoth && change.Current is not null)
                {
                    candidates.Add(new(change.Current.RelativePath, true, change.Current.Size, change.Current.LastWriteTimeUtc));
                }

                if (action is FileSyncDecision.DeleteOnClient or FileSyncDecision.DeleteBoth && change.Previous is not null)
                {
                    candidates.Add(new(change.Previous.RelativePath, false, change.Previous.Size, change.Previous.LastWriteTimeUtc));
                }
            }

            if (candidates.Count == 0)
            {
                return plan;
            }

            HashSet<DeletionCandidate> selected;
            if (ReviewDeletions is not null)
            {
                IReadOnlyList<DeletionCandidate>? result = ReviewDeletions(candidates);
                if (result is null)
                {
                    return null;
                }

                selected = new(result);
                if (selected.Count == 0 || selected.Any(item => !candidates.Contains(item)))
                {
                    return null;
                }
            }
            else
            {
                foreach (FileSyncDecision deletion in new[] { FileSyncDecision.DeleteOnServer, FileSyncDecision.DeleteOnClient, FileSyncDecision.DeleteBoth })
                {
                    string[] paths = plan.Where(item => item.Action == deletion)
                        .Select(item => (item.Change.Current ?? item.Change.Previous)!.RelativePath).ToArray();
                    string side = deletion == FileSyncDecision.DeleteBoth ? "на сервере и клиенте (все имеющиеся копии)"
                        : deletion == FileSyncDecision.DeleteOnServer ? "на сервере" : "на клиенте";
                    if (paths.Length > 0 && ConfirmDeletion?.Invoke(side, paths) != true)
                    {
                        return null;
                    }
                }

                return plan;
            }

            Dictionary<string, FileSyncDecision> revised = new(StringComparer.Ordinal);
            foreach (IGrouping<string, DeletionCandidate> group in candidates.GroupBy(item => item.Path, StringComparer.Ordinal))
            {
                bool server = group.Any(item => item.OnServer && selected.Contains(item));
                bool client = group.Any(item => !item.OnServer && selected.Contains(item));
                revised[group.Key] = server && client ? FileSyncDecision.DeleteBoth
                    : server ? FileSyncDecision.DeleteOnServer
                    : client ? FileSyncDecision.DeleteOnClient : FileSyncDecision.Skip;
            }

            Changes = Changes.Select(row => revised.TryGetValue(row.Path, out FileSyncDecision action)
                ? row with { Action = action, IsManualDecision = true } : row).ToArray();
            RefreshCommands();
            return plan.Select(item => (item.Change, revised.GetValueOrDefault(
                (item.Change.Current ?? item.Change.Previous)!.RelativePath, item.Action)))
                .Where(item => IsExecutable(item.Item2)).ToArray();
        }

        /// <summary>
        /// Проверяет принадлежность снимка сравнению и читает не более 8 МБ.
        /// </summary>
        public async Task<byte[]> LoadDeletionPreviewAsync(DeletionCandidate candidate, CancellationToken token)
        {
            ScanJobResponse comparison = _completedComparison ?? throw new InvalidOperationException("Повторите сравнение.");
            FileSnapshotDto? snapshot = comparison.Changes?.Select(change => candidate.OnServer ? change.Current : change.Previous)
                .SingleOrDefault(file => file?.RelativePath == candidate.Path);
            if (snapshot is null || snapshot.Size != candidate.Size || snapshot.LastWriteTimeUtc != candidate.ModifiedUtc)
            {
                throw new InvalidOperationException("Файл не относится к текущему сравнению.");
            }

            const int limit = 8_000_000;
            if (candidate.Size > limit)
            {
                throw new IOException("Просмотр содержимого доступен для файлов до 8 МБ. Атрибуты показаны в таблице.");
            }

            using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token);
            using PreviewBuffer output = new(limit);
            if (candidate.OnServer)
            {
                if (_jobClient is not IFileDownloadClient downloader)
                {
                    throw new InvalidOperationException("Сервер не поддерживает просмотр.");
                }

                await downloader.DownloadFileAsync(comparison.Id, candidate.Path, output, cancellation.Token);
            }
            else
            {
                await using Stream input = _fileReader.OpenRead(_comparedClientRoot,
                    new FileSnapshot(candidate.Path, candidate.Size, candidate.ModifiedUtc));
                await input.CopyToAsync(output, cancellation.Token);
            }

            if (output.Length != candidate.Size)
            {
                throw new IOException("Размер файла изменился. Повторите сравнение.");
            }

            return output.ToArray();
        }
    }

    /// <summary>
    /// Ограничивает объём загружаемого просмотра.
    /// </summary>
    internal sealed class PreviewBuffer(int limit) : MemoryStream
    {
        /// <summary>
        /// Проверяет лимит перед записью полученных данных.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Length + count > limit)
            {
                throw new IOException("Превышен лимит просмотра 8 МБ.");
            }

            base.Write(buffer, offset, count);
        }

        /// <summary>
        /// Ограничивает асинхронную запись из сетевого или файлового потока.
        /// </summary>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer.ToArray(), 0, buffer.Length);
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Проверяет лимит при записи массива.
        /// </summary>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }
    }
}