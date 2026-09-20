using HwSync.Abstractions.Models;

namespace HwSync.Windows.Contract.Client.Configuration
{
    /// <summary>
    /// Настройки пары папок и правил синхронизации.
    /// </summary>
    public sealed class SyncProfile
    {
        public string Id { get; init; } = "default";

        public string Name { get; init; } = "Основная папка";

        public string ServerAddress { get; init; } = "http://localhost:5080";

        public string ServerRootPath { get; init; } = "";

        public string ClientRootPath { get; init; } = "";

        public ConflictRules Rules { get; init; } = new();

        public ReconciliationRules Reconciliation { get; init; } = new();
    }
}
