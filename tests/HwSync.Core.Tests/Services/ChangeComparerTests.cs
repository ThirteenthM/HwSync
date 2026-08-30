using System.Threading.Channels;
using HwSync.Core.Models;
using HwSync.Core.Services;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HwSync.Core.Tests.Services
{
    public partial class ChangeComparerTests
    {
        [Test]
        public void Compare_WhenFileIsNew_ReturnsCreatedChange()
        {
            ChangeComparer comparer = new();

            FileSnapshot[] previous = [];
            FileSnapshot[] current = [CreateSnapshot()];

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

            FileSnapshot[] previous = [CreateSnapshot()];
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

            FileSnapshot[] previous = [CreateSnapshot()];
            FileSnapshot[] current = [CreateSnapshot(size: 1250, lastWriteTimeUtc: CreateLastWriteTimeUtc().AddMinutes(5))];

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

        [Test]
        public void Compare_WhenFileIsUnchanged_ReturnsNoChanges()
        {
            FileSnapshot[] previous = [CreateSnapshot()];
            FileSnapshot[] current = [CreateSnapshot()];

            ChangeComparer comparer = new();

            IReadOnlyCollection<FileChange> changes =
                comparer.Compare(previous, current);

            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void Compare_WhenFileSizeChanged_ReturnsModifiedChange()
        {
            FileSnapshot[] previous = [CreateSnapshot()];
            FileSnapshot[] current = [CreateSnapshot(size: 1250)];

            ChangeComparer comparer = new();

            IReadOnlyCollection<FileChange> changes =
                comparer.Compare(previous, current);

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes.Single().ChangeType,
                Is.EqualTo(FileChangeType.Modified));
        }

        [Test]
        public void Compare_WhenLastWriteTimeChanged_ReturnsModifiedChange()
        {
            FileSnapshot[] previous = [CreateSnapshot()];
            FileSnapshot[] current = [CreateSnapshot(lastWriteTimeUtc: CreateLastWriteTimeUtc().AddMinutes(5))];

            ChangeComparer comparer = new();

            IReadOnlyCollection<FileChange> changes = comparer.Compare(previous, current);

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes.Single().ChangeType,
                Is.EqualTo(FileChangeType.Modified));
        }

        [Test]
        public void Compare_W1()
        {
            string? s = null;
            string? s2 = s ?? string.Empty;

            Assert.That(s2, Is.Not.Null, "Строка не должна быть null");
        }
    }
}
