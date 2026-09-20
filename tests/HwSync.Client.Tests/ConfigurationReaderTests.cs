using HwSync.Windows.AppServices.Common.Configuration;
using System.IO;
using System.Text.Json;
using HwSync.Windows.AppServices.Client.Configuration;
using HwSync.Windows.Contract.Client.Configuration;

namespace HwSync.Client.Tests
{
    /// <summary>
    /// Проверки общей загрузки конфигурации и поведения наследников.
    /// </summary>
    public class ConfigurationReaderTests
    {
        /// <summary>
        /// Проверяет начальные настройки и профиль, когда файлов ещё нет.
        /// </summary>
        [Test]
        public void MissingFiles_UseDefaultsAndFallback()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".json");
            JsonConfigurationReader<ClientSettings> settingsReader = new ClientSettingsReader();
            ClientSettings defaults = settingsReader.Load(path);
            Assert.That(defaults.FileTransferTimeoutSeconds, Is.EqualTo(1800));
            ClientSettings fallback = new()
            {
                ServerAddress = "http://localhost:9000",
                ServerRootPath = "",
                ClientRootPath = ""
            };
            JsonConfigurationReader<List<SyncProfile>> profileReader = new SyncProfileReader(fallback);
            SyncProfile profile = profileReader.Load(path).Single();
            Assert.That(profile.ServerAddress, Is.EqualTo(fallback.ServerAddress));
            Assert.That(profile.ClientRootPath, Is.Empty);
            Assert.That(File.Exists(path), Is.False);
        }

        /// <summary>
        /// Проверяет общие параметры JSON и возможность передать собственные.
        /// </summary>
        [Test]
        public void Load_UsesDefaultOrSuppliedJsonOptions()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".json");
            File.WriteAllText(path, "{ /* timeout */ \"filetransfertimeoutseconds\":60, }");
            JsonConfigurationReader<ClientSettings> reader = new ClientSettingsReader();
            try
            {
                Assert.That(reader.Load(path).FileTransferTimeoutSeconds, Is.EqualTo(60));
                JsonSerializerOptions options = ClientSettingsReader.GetOptions();
                options.PropertyNameCaseInsensitive = false;
                Assert.That(reader.Load(path, options).FileTransferTimeoutSeconds, Is.EqualTo(1800));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Проверяет отказ обоих загрузчиков от null вместо конфигурации.
        /// </summary>
        [Test]
        public void Load_RejectsNullForBothReaders()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".json");
            File.WriteAllText(path, "null");
            try
            {
                JsonConfigurationReader<ClientSettings> settings = new ClientSettingsReader();
                JsonConfigurationReader<List<SyncProfile>> profiles = new SyncProfileReader(new());
                Assert.Throws<InvalidDataException>(() => settings.Load(path));
                Assert.Throws<InvalidDataException>(() => profiles.Load(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Проверяет вызов валидации профилей из базового Load.
        /// </summary>
        [Test]
        public void Load_ValidatesEmptyProfileArray()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".json");
            File.WriteAllText(path, "[]");
            try
            {
                JsonConfigurationReader<List<SyncProfile>> reader = new SyncProfileReader(new());
                Assert.Throws<InvalidDataException>(() => reader.Load(path));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
