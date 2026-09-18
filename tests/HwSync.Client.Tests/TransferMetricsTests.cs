using System.IO;
using HwSync.Client.Windows.Configuration;
using HwSync.Client.Windows.Diagnostics;

namespace HwSync.Client.Tests
{
    /// <summary>
    /// Проверки расчётов и настройки метрик копирования.
    /// </summary>
    public class TransferMetricsTests
    {
        /// <summary>
        /// Проверяет скорость по полному времени файла и исключение неуспешных попыток.
        /// </summary>
        [Test]
        public void Metrics_CountOnlyConfirmedBytes()
        {
            TransferMetrics metrics = new("Тест");
            metrics.Record("copied.bin", 2097152, TimeSpan.FromSeconds(2), true, "Скопирован");
            metrics.Record("failed.bin", 1048576, TimeSpan.FromSeconds(1), false, "Ошибка");
            metrics.Record("cancelled.bin", 1048576, TimeSpan.FromSeconds(1), false, "Прерван");
            metrics.Record("empty.bin", 0, TimeSpan.Zero, true, "Скопирован");
            metrics.Complete();
            Assert.That(metrics.ConfirmedBytes, Is.EqualTo(2097152));
            Assert.That(metrics.Files[0].MebibytesPerSecond, Is.EqualTo(1));
            Assert.That(metrics.Files[1].ConfirmedBytes, Is.Zero);
            Assert.That(metrics.Files[2].ConfirmedBytes, Is.Zero);
            Assert.That(metrics.Files[3].MebibytesPerSecond, Is.Zero);
            TimeSpan duration = metrics.Duration;
            Assert.That(metrics.Duration, Is.EqualTo(duration));
            Assert.That(metrics.Describe(), Does.Contain("файлов 2"));
        }

        /// <summary>
        /// Проверяет значение по умолчанию и явное отключение метрик в конфиге.
        /// </summary>
        [TestCase("{}", true)]
        [TestCase("{\"TransferMetricsEnabled\":true}", true)]
        [TestCase("{\"TransferMetricsEnabled\":false}", false)]
        public void Settings_ReadMetricsSwitch(string json, bool expected)
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".json");
            File.WriteAllText(path, json);
            try
            {
                Assert.That(ClientSettingsReader.Load(path).TransferMetricsEnabled, Is.EqualTo(expected));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
