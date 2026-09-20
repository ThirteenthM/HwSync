using HwSync.Windows.Contract.Client.Configuration;
using HwSync.Windows.Contract.Client.Services;
using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.AppServices.Client
{
    /// <summary>
    /// Применение правил профиля без изменения файлов.
    /// </summary>
    public sealed class SyncDecisionService : ISyncDecisionService
    {
        /// <summary>
        /// Оставляет неоднозначные различия на решение пользователя.
        /// </summary>
        public FileSyncDecision Decide(bool existsOnClient, bool existsOnServer, ConflictRules rules)
        {
            if (existsOnClient && existsOnServer)
            {
                return rules.DifferentFiles switch
                {
                    SyncRule.Skip or SyncRule.Keep => FileSyncDecision.Skip,
                    SyncRule.AskUser => FileSyncDecision.AskUser,
                    SyncRule.KeepBoth => FileSyncDecision.KeepBoth,
                    _ => FileSyncDecision.AskUser
                };
            }

            if (existsOnServer)
            {
                return rules.MissingOnClient switch
                {
                    SyncRule.Copy => FileSyncDecision.CopyToClient,
                    SyncRule.Delete => FileSyncDecision.DeleteOnServer,
                    SyncRule.Keep or SyncRule.Skip => FileSyncDecision.Skip,
                    _ => FileSyncDecision.AskUser
                };
            }

            if (existsOnClient)
            {
                return rules.ClientOnlyFiles switch
                {
                    SyncRule.Copy => FileSyncDecision.CopyToServer,
                    SyncRule.Delete => FileSyncDecision.DeleteOnClient,
                    SyncRule.Keep or SyncRule.Skip => FileSyncDecision.Skip,
                    _ => FileSyncDecision.AskUser
                };
            }

            return FileSyncDecision.AskUser;
        }

        /// <summary>
        /// Не восстанавливает известное удаление автоматически и выделяет конфликт изменения.
        /// </summary>
        public FileSyncDecision Decide(HwSync.Abstractions.Models.FileSnapshot? client, HwSync.Abstractions.Models.FileSnapshot? server,
            FileDeletionEvidence? clientDeletion, FileDeletionEvidence? serverDeletion, ConflictRules rules)
        {
            if (client is not null && server is null && serverDeletion is not null)
            {
                return DecideDeletion(client, serverDeletion, rules.ServerDeletions, FileSyncDecision.DeleteOnClient);
            }

            if (server is not null && client is null && clientDeletion is not null)
            {
                return DecideDeletion(server, clientDeletion, rules.ClientDeletions, FileSyncDecision.DeleteOnServer);
            }

            return Decide(client is not null, server is not null, rules);
        }

        /// <summary>
        /// Применяет удаление только при совпадении известных атрибутов версии.
        /// </summary>
        private static FileSyncDecision DecideDeletion(HwSync.Abstractions.Models.FileSnapshot remaining,
            FileDeletionEvidence deletion, SyncRule rule, FileSyncDecision deleteAction)
        {
            if (deletion.PreviousFile is not { } previous
                || !StringComparer.OrdinalIgnoreCase.Equals(previous.RelativePath, remaining.RelativePath)
                || previous.Size != remaining.Size || previous.LastWriteTimeUtc != remaining.LastWriteTimeUtc)
            {
                return FileSyncDecision.AskUser;
            }

            return rule switch
            {
                SyncRule.Delete => deleteAction,
                SyncRule.Keep or SyncRule.Skip or SyncRule.RecordOnly => FileSyncDecision.Skip,
                _ => FileSyncDecision.AskUser
            };
        }    }
}
