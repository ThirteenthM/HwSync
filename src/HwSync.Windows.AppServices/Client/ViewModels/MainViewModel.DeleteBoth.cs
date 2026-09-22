using System.IO;
using HwSync.Windows.Contract.Client.ViewModels;
using HwSync.Abstractions.Models;
using HwSync.Api.Client;
using HwSync.Api.Contracts;

namespace HwSync.Windows.AppServices.Client.ViewModels
{
    /// <summary>
    /// Удаление имеющихся копий с отчётом о частичном выполнении.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>
        /// Проверяет снимки и удаляет серверную копию перед клиентской.
        /// </summary>
        private async Task DeleteBothAsync(Guid comparisonId, FileChangeDto change, CancellationToken token)
        {
            FileSnapshotDto snapshot = (change.Previous ?? change.Current)!;
            FileSnapshot file = new(snapshot.RelativePath, snapshot.Size, snapshot.LastWriteTimeUtc);
            if (change.Previous is null || change.Current is null)
            {
                await ApplyPlannedActionAsync(comparisonId, file,
                    change.Previous is null ? FileSyncDecision.DeleteOnServer
                        : FileSyncDecision.DeleteOnClient, token);
                return;
            }

            using (Stream source = _fileReader.OpenRead(_comparedClientRoot, file))
            {
                token.ThrowIfCancellationRequested();
            }

            try
            {
                await ((IComparedFileMutationClient)_jobClient!).DeleteComparedServerFileAsync(comparisonId, file.RelativePath, token);
            }
            catch (Exception exception)
            {
                throw new IOException($"{file.RelativePath}: удаление на сервере не подтверждено. Клиентская копия не удалялась. Повторите сравнение. {exception.Message}", exception);
            }

            try
            {
                token.ThrowIfCancellationRequested();
                _fileOperations.DeleteUnchanged(_comparedClientRoot, file);
            }
            catch (Exception exception)
            {
                throw new IOException($"{file.RelativePath}: серверная копия удалена; удаление клиентской копии не завершено. Повторите сравнение. {exception.Message}", exception);
            }
        }
    }
}