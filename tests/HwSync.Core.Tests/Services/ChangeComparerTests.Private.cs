using HwSync.Abstractions.Models;

namespace HwSync.Core.Tests.Services
{
    /// <summary>Проверки обнаружения изменений атрибутов файлов.</summary>
    public partial class ChangeComparerTests
    {
        private const string TestRelativePath = "Photos/2026/Test.jpg";

        /// <summary>Создаёт снимок с атрибутами для сценария теста.</summary>
        private static FileSnapshot CreateSnapshot(
            long size = 1000,
            DateTime? lastWriteTimeUtc = null) =>
            new(TestRelativePath, size, lastWriteTimeUtc ?? CreateLastWriteTimeUtc());

        /// <summary>Создаёт воспроизводимое время изменения для тестов.</summary>
        private static DateTime CreateLastWriteTimeUtc() => new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
    }
}
