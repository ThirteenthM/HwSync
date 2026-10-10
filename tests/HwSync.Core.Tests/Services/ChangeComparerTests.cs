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

        /// <summary>
        /// Сопоставляет Windows-пути без учёта регистра и сохраняет написание обеих сторон.
        /// </summary>
        [TestCase("docs/Report.txt", "DOCS/report.txt", false)]
        [TestCase("docs/Report.txt", "DOCS/report.txt", true)]
        [TestCase("Документы/Отчёт.txt", "документы/ОТЧЁТ.txt", false)]
        [TestCase("Документы/Отчёт.txt", "документы/ОТЧЁТ.txt", true)]
        public void Compare_PathCaseDiffers_ReturnsSinglePair(string clientPath, string serverPath, bool modified)
        {
            IChangeComparer comparer = new ChangeComparer();
            FileSnapshot client = new(clientPath, 10, CreateLastWriteTimeUtc());
            FileSnapshot server = new(serverPath, modified ? 20 : 10, CreateLastWriteTimeUtc());

            IReadOnlyCollection<FileChange> changes = comparer.Compare([client], [server]);

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes.Single(), Is.EqualTo(new FileChange(
                modified ? FileChangeType.Modified : FileChangeType.Unchanged, client, server)));
        }

        /// <summary>
        /// Не выбирает произвольно одну из двух записей одного Windows-пути.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void Compare_DuplicateWindowsPaths_RejectsSnapshot(bool inPrevious)
        {
            IChangeComparer comparer = new ChangeComparer();
            FileSnapshot[] ambiguous =
            [
                new("docs/Report.txt", 10, CreateLastWriteTimeUtc()),
                new("DOCS/report.txt", 20, CreateLastWriteTimeUtc())
            ];

            Assert.Throws<ArgumentException>(() => comparer.Compare(
                inPrevious ? ambiguous : [], inPrevious ? [] : ambiguous));
        }
    }
}
