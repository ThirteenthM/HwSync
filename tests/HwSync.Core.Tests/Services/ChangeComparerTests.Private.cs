using HwSync.Abstractions.Models;
using HwSync.Core.Models;

namespace HwSync.Core.Tests.Services
{
    public partial class ChangeComparerTests
    {
        private const string TestRelativePath = "Photos/2026/Test.jpg";

        private static FileSnapshot CreateSnapshot(
            long size = 1000,
            DateTime? lastWriteTimeUtc = null) =>
            new(TestRelativePath, size, lastWriteTimeUtc ?? CreateLastWriteTimeUtc());

        private static DateTime CreateLastWriteTimeUtc() => new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
    }
}
