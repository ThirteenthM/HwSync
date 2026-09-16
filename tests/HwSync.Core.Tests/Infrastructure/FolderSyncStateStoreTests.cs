using HwSync.Abstractions.FileSystem;
using HwSync.Abstractions.Models;
using HwSync.Infrastructure.FileSystem;

namespace HwSync.Core.Tests.Infrastructure
{
    public class FolderSyncStateStoreTests
    {
        [Test]
        public void Save_ReloadsAcknowledgedVersionsAndSeparatesClientsAndFolders()
        {
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "sync-state-tests", Guid.NewGuid().ToString("N"));
            Guid clientId = Guid.NewGuid();
            IFolderSyncStateStore store = new JsonFolderSyncStateStore(directory);
            Assert.That(store.Load(clientId, "folder"), Is.Null);
            FolderSyncState state = new(clientId, "folder", [new("file.txt", 4, new string('A', 64)), new("deleted.txt", 5, null)]);
            store.Save(state);
            IFolderSyncStateStore reopened = new JsonFolderSyncStateStore(directory);
            FolderSyncState? loaded = reopened.Load(clientId, "folder");
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Files, Is.EqualTo(state.Files));
            Assert.That(reopened.Load(Guid.NewGuid(), "folder"), Is.Null);
            Assert.That(reopened.Load(clientId, "another-folder"), Is.Null);
            store.Save(state with { Files = [new("file.txt", 6, new string('B', 64))] });
            Assert.That(reopened.Load(clientId, "folder")!.Files.Single().Revision, Is.EqualTo(6));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }

        [Test]
        public void Load_CorruptedStateDoesNotBecomeEmptyBaseline()
        {
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "sync-state-tests", Guid.NewGuid().ToString("N"));
            Guid clientId = Guid.NewGuid();
            IFolderSyncStateStore store = new JsonFolderSyncStateStore(directory);
            store.Save(new(clientId, "folder", []));
            File.WriteAllText(Directory.GetFiles(directory, "*.json").Single(), "null");
            Assert.Throws<InvalidDataException>(() => store.Load(clientId, "folder"));
        }
    }
}
