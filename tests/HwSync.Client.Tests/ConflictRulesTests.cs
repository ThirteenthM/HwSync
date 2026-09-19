using System.IO;
using System.Text.Json;
using HwSync.Client.Windows.Application.Configuration;
using HwSync.Client.Windows.Contract.Configuration;

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
                ConflictRules rules = new SyncProfileReader(new()).Load(path).Single().Rules;
                Assert.That(rules.MissingOnClient, Is.EqualTo(SyncRule.Copy));
                Assert.That(rules.DifferentFiles, Is.EqualTo(includeRules ? SyncRule.Skip : SyncRule.AskUser));
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
        [TestCase("ClientOnlyFiles", "Unknown")]
        [TestCase("ServerDeletions", "Unknown")]
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
                Assert.Throws<JsonException>(() => new SyncProfileReader(new()).Load(path));
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
        [TestCase("ClientOnlyFiles", "RecordOnly")]
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
                Assert.Throws<InvalidDataException>(() => new SyncProfileReader(new()).Load(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Загружает поддерживаемые действия автоматического плана из JSON.
        /// </summary>
        [TestCase("MissingOnClient", "Delete")]
        [TestCase("MissingOnClient", "Skip")]
        [TestCase("ClientOnlyFiles", "Copy")]
        [TestCase("ClientOnlyFiles", "Delete")]
        [TestCase("ClientOnlyFiles", "Skip")]
        [TestCase("DifferentFiles", "Keep")]
        [TestCase("DifferentFiles", "AskUser")]
        public void Load_AcceptsAutomaticRules(string property, string value)
        {
            Dictionary<string, object> profile = CreateProfile();
            profile["Rules"] = new Dictionary<string, object> { [property] = value };
            string path = WriteProfile(profile);
            try
            {
                ConflictRules rules = new SyncProfileReader(new()).Load(path).Single().Rules;
                SyncRule actual = property switch
                {
                    "MissingOnClient" => rules.MissingOnClient,
                    "ClientOnlyFiles" => rules.ClientOnlyFiles,
                    _ => rules.DifferentFiles
                };
                Assert.That(actual, Is.EqualTo(Enum.Parse<SyncRule>(value)));
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
