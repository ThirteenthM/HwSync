using HwSync.Abstractions.Models;
using HwSync.Abstractions.Services;
using HwSync.Core.Services;

namespace HwSync.Core.Tests.Services
{
    /// <summary>
    /// Проверки одностороннего плана синхронизации.
    /// </summary>
    public class SyncPlannerTests
    {
        /// <summary>
        /// Проверяет действия выбранного режима синхронизации.
        /// </summary>
        [TestCase(SyncMode.Update, SyncAction.KeepOnClient)]
        [TestCase(SyncMode.Mirror, SyncAction.DeleteFromClient)]
        public void Create_MapsDifferencesToDirectionalActions(SyncMode mode, SyncAction clientOnlyAction)
        {
            FileSnapshot serverOnly = new("new.txt", 10, DateTime.UnixEpoch);
            FileSnapshot old = new("changed.txt", 5, DateTime.UnixEpoch);
            FileSnapshot updated = old with
            {
                Size = 20
            };
            FileSnapshot clientOnly = new("local.txt", 3, DateTime.UnixEpoch);
            ISyncPlanner planner = new SyncPlanner();
            SyncPlan plan = planner.Create([
                new(FileChangeType.Created, null, serverOnly),
                new(FileChangeType.Modified, old, updated),
                new(FileChangeType.Deleted, clientOnly, null)], mode);
            Assert.That(plan.Items.Select(item => item.Action), Is.EqualTo(new[]
                {
 SyncAction.CopyToClient, SyncAction.ReplaceOnClient, clientOnlyAction
}));
            Assert.That(plan.Items[1].ClientFile, Is.EqualTo(old));
            Assert.That(plan.Items[1].ServerFile, Is.EqualTo(updated));
        }

        /// <summary>
        /// Проверяет запрет путей за пределами корня.
        /// </summary>
        [TestCase("../outside.txt")]
        [TestCase("C:/outside.txt")]
        [TestCase("/outside.txt")]
        public void Create_RejectsPathsOutsideRoot(string path)
        {
            ISyncPlanner planner = new SyncPlanner();
            Assert.Throws<ArgumentException>(() => planner.Create([
                new(FileChangeType.Created, null, new(path, 1, DateTime.UnixEpoch))], SyncMode.Update));
        }

        /// <summary>
        /// Планирует одну операцию для пары путей с разным регистром.
        /// </summary>
        [TestCase(FileChangeType.Unchanged, SyncMode.Update, SyncAction.KeepOnClient)]
        [TestCase(FileChangeType.Modified, SyncMode.Update, SyncAction.ReplaceOnClient)]
        [TestCase(FileChangeType.Modified, SyncMode.Mirror, SyncAction.ReplaceOnClient)]
        [TestCase(FileChangeType.Modified, SyncMode.CopyMissing, SyncAction.KeepOnClient)]
        public void Create_PathCaseDiffers_AcceptsPair(FileChangeType kind, SyncMode mode, SyncAction action)
        {
            FileSnapshot client = new("docs/Report.txt", 10, DateTime.UnixEpoch);
            FileSnapshot server = new("DOCS/report.txt", kind == FileChangeType.Modified ? 20 : 10, DateTime.UnixEpoch);
            ISyncPlanner planner = new SyncPlanner();

            SyncPlan plan = planner.Create([new(kind, client, server)], mode);

            Assert.That(plan.Items.Single().Action, Is.EqualTo(action));
            Assert.That(plan.Items.Single().ClientFile, Is.EqualTo(client));
            Assert.That(plan.Items.Single().ServerFile, Is.EqualTo(server));
        }
    }
}
