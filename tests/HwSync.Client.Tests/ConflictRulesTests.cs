using System.IO;
using System.Text.Json;
using HwSync.Client.Windows.Configuration;

namespace HwSync.Client.Tests
{
    /// <summary>
    /// Проверки типизированных правил и совместимости JSON-профилей.
    /// </summary>
    public class ConflictRulesTests
    {
        /// <summary>
        /// Проверяет прежние строковые значения JSON и значения по умолчанию.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public void Load_PreservesExistingRules(bool includeRules)
        {
            Dictionary<string, object> profile = CreateProfile();
            if (includeRules)
            {
                profile["Rules"] = new
                {
                    MissingOnClient = "Copy",
                    DifferentFiles = "Skip",
                    ClientOnlyFiles = "Keep",
                    ServerDeletions = "RecordOnly"
                };
            }
            string path = WriteProfile(profile);
            try
            {
                ConflictRules rules = SyncProfileReader.Load(path, new()).Single().Rules;
                Assert.That(rules.MissingOnClient, Is.EqualTo(SyncRule.Copy));
                Assert.That(rules.DifferentFiles, Is.EqualTo(SyncRule.Skip));
                Assert.That(rules.ClientOnlyFiles, Is.EqualTo(SyncRule.Keep));
                Assert.That(rules.ServerDeletions, Is.EqualTo(SyncRule.RecordOnly));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Проверяет отказ при опечатке или числовом значении вместо имени правила.
        /// </summary>
        [TestCase("MissingOnClient", "Coppy")]
        [TestCase("DifferentFiles", "Replace")]
        [TestCase("ClientOnlyFiles", "Delete")]
        [TestCase("ServerDeletions", "Delete")]
        [TestCase("MissingOnClient", 0)]
        [TestCase("DifferentFiles", 0)]
        [TestCase("ClientOnlyFiles", 0)]
        [TestCase("ServerDeletions", 0)]
        public void Load_RejectsUnsupportedValues(string property, object value)
        {
            Dictionary<string, object> profile = CreateProfile();
            profile["Rules"] = new Dictionary<string, object>
            {
                [property] = value
            };
            string path = WriteProfile(profile);
            try
            {
                Assert.Throws<JsonException>(() => SyncProfileReader.Load(path, new()));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Проверяет отказ для известного правила в неподходящей настройке.
        /// </summary>
        [TestCase("MissingOnClient", "RecordOnly")]
        [TestCase("DifferentFiles", "Copy")]
        [TestCase("ClientOnlyFiles", "Skip")]
        [TestCase("ServerDeletions", "Keep")]
        public void Load_RejectsInvalidRuleCombination(string property, string value)
        {
            Dictionary<string, object> profile = CreateProfile();
            profile["Rules"] = new Dictionary<string, object>
            {
                [property] = value
            };
            string path = WriteProfile(profile);
            try
            {
                Assert.Throws<InvalidDataException>(() => SyncProfileReader.Load(path, new()));
            }
            finally
            {
                File.Delete(path);
            }
        }
        /// <summary>
        /// Создаёт профиль с допустимыми абсолютными путями.
        /// </summary>
        private static Dictionary<string, object> CreateProfile() => new()
        {
            ["Id"] = "test",
            ["Name"] = "Тест",
            ["ServerAddress"] = "http://localhost:5080",
            ["ServerRootPath"] = Path.GetFullPath("server"),
            ["ClientRootPath"] = Path.GetFullPath("client")
        };

        /// <summary>
        /// Сохраняет тестовый профиль в отдельный временный JSON-файл.
        /// </summary>
        private static string WriteProfile(Dictionary<string, object> profile)
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(new[] { profile }));
            return path;
        }
    }
}
