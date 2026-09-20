using HwSync.Windows.Contract.Client.Configuration;
using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.Contract.Client.Services
{
    /// <summary>
    /// Выбор действия по наличию файлов и правилам профиля.
    /// </summary>
    public interface ISyncDecisionService
    {
        /// <summary>
        /// Предлагает действие для различающейся пары файлов.
        /// </summary>
        FileSyncDecision Decide(bool existsOnClient, bool existsOnServer, ConflictRules rules);

        /// <summary>
        /// Учитывает известное удаление до правил для односторонних файлов.
        /// </summary>
        FileSyncDecision Decide(HwSync.Abstractions.Models.FileSnapshot? client, HwSync.Abstractions.Models.FileSnapshot? server,
            FileDeletionEvidence? clientDeletion, FileDeletionEvidence? serverDeletion, ConflictRules rules);
    }
}
