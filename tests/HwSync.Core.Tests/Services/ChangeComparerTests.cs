using HwSync.Abstractions.Services;
using HwSync.Abstractions.Models;
using HwSync.Core.Services;

namespace HwSync.Core.Tests.Services
{
    /// <summary>
    /// Проверки обнаружения изменений атрибутов файлов.
    /// </summary>
    public partial class ChangeComparerTests
    {
        /// <summary>
        /// Проверяет обнаружение нового файла.
        /// </summary>
        [Test]
        public void Compare_WhenFileIsNew_ReturnsCreatedChange()
        {
            IChangeComparer comparer = new ChangeComparer();

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

        /// <summary>
        /// Проверяет обнаружение исчезнувшего файла.
        /// </summary>
        [Test]
        public void Compare_WhenFileIsDeleted_ReturnsDeletedChange()
        {
            IChangeComparer comparer = new ChangeComparer();

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

        /// <summary>
        /// Проверяет обнаружение изменённого файла.
        /// </summary>
        [Test]
        public void Compare_WhenFileIsModified_ReturnsModifiedChange()
        {
            IChangeComparer comparer = new ChangeComparer();

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

        /// <summary>
        /// Проверяет отсутствие различий у одинаковых снимков.
        /// </summary>
        [Test]
        public void Compare_WhenFileIsUnchanged_ReturnsBothSnapshots()
        {
            FileSnapshot[] previous = [CreateSnapshot()];
            FileSnapshot[] current = [CreateSnapshot()];

            IChangeComparer comparer = new ChangeComparer();

            IReadOnlyCollection<FileChange> changes =
                comparer.Compare(previous, current);

            Assert.That(changes.Single(), Is.EqualTo(new FileChange(FileChangeType.Unchanged, previous[0], current[0])));
        }

        /// <summary>
        /// Проверяет обнаружение изменения размера.
        /// </summary>
        [Test]
        public void Compare_WhenFileSizeChanged_ReturnsModifiedChange()
        {
            FileSnapshot[] previous = [CreateSnapshot()];
            FileSnapshot[] current = [CreateSnapshot(size: 1250)];

            IChangeComparer comparer = new ChangeComparer();

            IReadOnlyCollection<FileChange> changes =
                comparer.Compare(previous, current);

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes.Single().ChangeType,
                Is.EqualTo(FileChangeType.Modified));
        }

        /// <summary>
        /// Разный регистр и разделитель не делят один файл на создание и удаление.
        /// </summary>
        [Test]
        public void Compare_WhenPathDiffersByCaseOrSeparator_ReturnsSingleChange()
        {
            FileSnapshot previous = new(@"Photos\A.jpg", 10, CreateLastWriteTimeUtc());
            FileSnapshot current = new("photos/a.jpg", 10, CreateLastWriteTimeUtc());
            IChangeComparer comparer = new ChangeComparer();

            IReadOnlyCollection<FileChange> changes = comparer.Compare([previous], [current]);

            Assert.That(changes.Single(), Is.EqualTo(new FileChange(FileChangeType.Unchanged, previous, current)));
        }

        /// <summary>
        /// Повтор одного пути в снимке отклоняется.
        /// </summary>
        [Test]
        public void Compare_WhenSnapshotRepeatsPath_Throws()
        {
            FileSnapshot first = new("A.txt", 1, CreateLastWriteTimeUtc());
            FileSnapshot second = new("a.txt", 1, CreateLastWriteTimeUtc());
            IChangeComparer comparer = new ChangeComparer();

            Assert.Throws<ArgumentException>(() => comparer.Compare([first, second], []));
        }

        /// <summary>
        /// Проверяет обнаружение изменения времени записи.
        /// </summary>
        [Test]
        public void Compare_WhenLastWriteTimeChanged_ReturnsModifiedChange()
        {
            FileSnapshot[] previous = [CreateSnapshot()];
            FileSnapshot[] current = [CreateSnapshot(lastWriteTimeUtc: CreateLastWriteTimeUtc().AddMinutes(5))];

            IChangeComparer comparer = new ChangeComparer();

            IReadOnlyCollection<FileChange> changes = comparer.Compare(previous, current);

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes.Single().ChangeType,
                Is.EqualTo(FileChangeType.Modified));
        }
    }
}
