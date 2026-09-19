using HwSync.Client.Windows.Contract.Configuration;
using HwSync.Client.Windows.Contract.ViewModels;

namespace HwSync.Client.Windows.Contract.Services
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
    }
}