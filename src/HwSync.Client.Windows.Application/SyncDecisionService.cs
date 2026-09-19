using HwSync.Client.Windows.Contract.Configuration;
using HwSync.Client.Windows.Contract.Services;
using HwSync.Client.Windows.Contract.ViewModels;

namespace HwSync.Client.Windows.Application
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
    }
}