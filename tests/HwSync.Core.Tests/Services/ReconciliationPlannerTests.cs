using HwSync.Abstractions.Models;
using HwSync.Abstractions.Services;
using HwSync.Core.Services;

namespace HwSync.Core.Tests.Services
{
    public class ReconciliationPlannerTests
    {
        private static readonly Guid ClientId = Guid.NewGuid();

        [TestCase(null, "A", null, ReconciliationAction.Download)]
        [TestCase(null, null, "A", ReconciliationAction.Upload)]
        [TestCase(null, "A", "B", ReconciliationAction.KeepBoth)]
        [TestCase("A", "A", "A", ReconciliationAction.None)]
        [TestCase("A", "B", "B", ReconciliationAction.None)]
        [TestCase("A", "B", "A", ReconciliationAction.Download)]
        [TestCase("A", "A", "B", ReconciliationAction.Upload)]
        [TestCase("A", "B", "C", ReconciliationAction.KeepBoth)]
        [TestCase("A", null, "A", ReconciliationAction.DeleteOnClient)]
        [TestCase("A", "A", null, ReconciliationAction.DeleteOnServer)]
        [TestCase("A", null, "B", ReconciliationAction.NeedsDecision)]
        [TestCase("A", "B", null, ReconciliationAction.NeedsDecision)]
        [TestCase("A", null, null, ReconciliationAction.None)]
        public void Create_UsesCommonBaseline(string? previous, string? server, string? client,
            ReconciliationAction expected)
        {
            IReconciliationPlanner planner = new ReconciliationPlanner();
            FolderSyncState baseline = new(ClientId, "folder", Files(previous, 1));
            ReconciliationPlan plan = planner.Create(baseline, Files(server, 2), Files(client, 0), new());
            Assert.That(plan.Items.Single().Action, Is.EqualTo(expected));
        }

        [Test]
        public void Create_NewClientWithOldCopyAndServerTombstone_RequiresDecision()
        {
            IReconciliationPlanner planner = new ReconciliationPlanner();
            ReconciliationPlan plan = planner.Create(new(ClientId, "folder", []),
                [new("file.txt", 2, null)], Files("A", 0), new());
            Assert.That(plan.Items.Single().Action, Is.EqualTo(ReconciliationAction.NeedsDecision));
        }

        [Test]
        public void Create_AcknowledgedDeletionAndNewFile_UploadsRecreatedFile()
        {
            IReconciliationPlanner planner = new ReconciliationPlanner();
            ReconciliationPlan plan = planner.Create(new(ClientId, "folder", [new("file.txt", 2, null)]),
                [new("file.txt", 2, null)], Files("B", 0), new());
            Assert.That(plan.Items.Single().Action, Is.EqualTo(ReconciliationAction.Upload));
        }

        [Test]
        public void Create_RejectsRolledBackServer()
        {
            IReconciliationPlanner planner = new ReconciliationPlanner();
            Assert.Throws<ArgumentException>(() => planner.Create(new(ClientId, "folder", Files("A", 3)),
                Files("B", 2), Files("A", 0), new()));
        }

        [Test]
        public void Create_NormalizesWindowsPathsAndHashCase()
        {
            IReconciliationPlanner planner = new ReconciliationPlanner();
            ReconciliationPlan plan = planner.Create(new(ClientId, "folder", []),
                [new("sub\\file.txt", 1, new string('a', 64))],
                [new("SUB/file.txt", 0, new string('A', 64))], new());
            Assert.That(plan.Items.Single().Action, Is.EqualTo(ReconciliationAction.None));
        }

        [TestCase("../file.txt")]
        [TestCase("C:/file.txt")]
        [TestCase("/file.txt")]
        [TestCase("sub./file.txt")]
        public void Create_RejectsUnsafePaths(string path)
        {
            IReconciliationPlanner planner = new ReconciliationPlanner();
            Assert.Throws<ArgumentException>(() => planner.Create(new(ClientId, "folder", []),
                [new(path, 1, new string('A', 64))], [], new()));
        }

        private static FileVersion[] Files(string? hash, long revision) =>
            hash is null ? [] : [new("file.txt", revision, new string(hash[0], 64))];
    }
}
