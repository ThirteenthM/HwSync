using HwSync.Core.Models;
using HwSync.Core.Services;

namespace HwSync.Core.Tests.Services
{
    public class ChangeComparerTests
    {
        [Test]
        public void Compare_WhenFileIsNew_ReturnsCreatedChange()
        {
            ChangeComparer comparer = new();

            FileSnapshot[] previous = [];

            FileSnapshot[] current =
            [
                new(
                    "Photos/2026/Test.jpg",
                    1000,
                    new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc)
                ),
            ];

            IReadOnlyCollection<FileChange> changes = comparer.Compare(previous, current);

            Assert.That(changes, Has.Count.EqualTo(1));

            FileChange change = changes.Single();

            Assert.Multiple(() =>
            {
                Assert.That(change.ChangeType, Is.EqualTo(FileChangeType.Created));
                Assert.That(change.Previous, Is.Null);
                Assert.That(change.Current, Is.EqualTo(current[0]));
            });
        }

        [Test]
        public void Compare_WhenFileIsDeleted_ReturnsDeletedChange()
        {
            ChangeComparer comparer = new();

            FileSnapshot[] previous =
            [
                new(
                    "Photos/2026/Test.jpg",
                    1000,
                    new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc)
                ),
            ];

            FileSnapshot[] current = [];

            IReadOnlyCollection<FileChange> changes = comparer.Compare(previous, current);

            Assert.That(changes, Has.Count.EqualTo(1));

            FileChange change = changes.Single();

            Assert.Multiple(() =>
            {
                Assert.That(change.ChangeType, Is.EqualTo(FileChangeType.Deleted));
                Assert.That(change.Previous, Is.EqualTo(previous[0]));
                Assert.That(change.Current, Is.Null);
            });
        }

        [Test]
        public void Compare_WhenFileIsModified_ReturnsModifiedChange()
        {
            ChangeComparer comparer = new();

            FileSnapshot[] previous =
            [
                new(
                    "Photos/2026/Test.jpg",
                    1000,
                    new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc)
                ),
            ];

            FileSnapshot[] current =
            [
                new(
                    "Photos/2026/Test.jpg",
                    1250,
                    new DateTime(2026, 8, 9, 12, 5, 0, DateTimeKind.Utc)
                ),
            ];

            IReadOnlyCollection<FileChange> changes = comparer.Compare(previous, current);

            Assert.That(changes, Has.Count.EqualTo(1));

            FileChange change = changes.Single();

            Assert.Multiple(() =>
            {
                Assert.That(change.ChangeType, Is.EqualTo(FileChangeType.Modified));
                Assert.That(change.Previous, Is.EqualTo(previous[0]));
                Assert.That(change.Current, Is.EqualTo(current[0]));
            });
        }
    }
}
