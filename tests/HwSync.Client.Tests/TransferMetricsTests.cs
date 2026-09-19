using System.IO;
using HwSync.Client.Windows.Application.Configuration;
using HwSync.Client.Windows.Contract.Configuration;
using HwSync.Client.Windows.Application.Diagnostics;
using HwSync.Client.Windows.Contract.Diagnostics;

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
            Assert.That(metrics.Files[0].MegabytesPerSecond, Is.EqualTo(1.048576));
            Assert.That(metrics.Files[1].ConfirmedBytes, Is.Zero);
            Assert.That(metrics.Files[2].ConfirmedBytes, Is.Zero);
            Assert.That(metrics.Files[3].MegabytesPerSecond, Is.Zero);
            Assert.That(metrics.MegabytesPerSecond, Is.EqualTo(2.097152 / metrics.Duration.TotalSeconds).Within(0.000001));
            TimeSpan duration = metrics.Duration;
            Assert.That(metrics.Duration, Is.EqualTo(duration));
            Assert.That(metrics.Describe(), Does.Contain("файлов 2"));
        }

        /// <summary>
        /// Проверяет десятичные мегабайты, дробное время и нулевую длительность.
        /// </summary>
        [TestCase(2500000L, 0.5, 5)]
        [TestCase(1000000L, 2, 0.5)]
        [TestCase(5000000000L, 10, 500)]
        [TestCase(0L, 1, 0)]
        [TestCase(1000000L, 0, 0)]
        public void Measurement_CalculatesMegabytesPerSecond(long bytes, double seconds, double expected)
        {
            FileTransferMeasurement measurement = new("file.bin", bytes, TimeSpan.FromSeconds(seconds), "Скопирован");
            Assert.That(measurement.MegabytesPerSecond, Is.EqualTo(expected));
            Assert.That(measurement.Volume, Is.EqualTo($"{bytes / 1_000_000d:F2} МБ"));
            Assert.That(measurement.Speed, Is.EqualTo(bytes > 0 ? $"{expected:F2} МБ/с" : "—"));
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
                Assert.That(new ClientSettingsReader().Load(path).TransferMetricsEnabled, Is.EqualTo(expected));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
