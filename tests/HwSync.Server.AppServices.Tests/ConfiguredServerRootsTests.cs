using HwSync.Server.AppServices;
using Microsoft.Extensions.Configuration;

namespace HwSync.Server.AppServices.Tests
{
    /// <summary>
    /// Проверки списка разрешённых каталогов сервера.
    /// </summary>
    public sealed class ConfiguredServerRootsTests
    {
        /// <summary>
        /// Разрешает сам корень и каталог внутри него, отклоняет соседний каталог.
        /// </summary>
        [Test]
        public void EnsureAllowed_AcceptsRootAndChildOnly()
        {
            string root = Path.Combine(Path.GetTempPath(), "HwSync-Roots-" + Guid.NewGuid().ToString("N"));
            string child = Path.Combine(root, "child");
            string sibling = Path.Combine(Path.GetTempPath(), "HwSync-Sibling-" + Guid.NewGuid().ToString("N"));
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:AllowedRoots:0"] = root + Path.DirectorySeparatorChar
                })
                .Build();
            ConfiguredServerRoots roots = new(configuration);

            Assert.DoesNotThrow(() => roots.EnsureAllowed(root));
            Assert.DoesNotThrow(() => roots.EnsureAllowed(child));
            ArgumentException error = Assert.Throws<ArgumentException>(() => roots.EnsureAllowed(sibling))!;
            Assert.That(error.Message, Does.Contain("не входит"));
        }

        /// <summary>
        /// Пустой список и относительный путь в настройке не открывают произвольный каталог.
        /// </summary>
        [Test]
        public void EnsureAllowed_EmptyListAndRelativeConfiguration_AreRejected()
        {
            IConfiguration empty = new ConfigurationBuilder().Build();
            ConfiguredServerRoots roots = new(empty);
            Assert.Throws<ArgumentException>(() => roots.EnsureAllowed(Path.GetTempPath()));

            IConfiguration relative = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:AllowedRoots:0"] = "relative"
                })
                .Build();
            Assert.Throws<InvalidOperationException>(() => new ConfiguredServerRoots(relative));
        }
    }
}
