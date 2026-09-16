using HwSync.Abstractions.Models;

namespace HwSync.Client.Windows.Configuration
{
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

    public sealed class ConflictRules
    {
        public string MissingOnClient { get; init; } = "Copy";
        public string DifferentFiles { get; init; } = "Skip";
        public string ClientOnlyFiles { get; init; } = "Keep";
        public string ServerDeletions { get; init; } = "RecordOnly";
    }
}
