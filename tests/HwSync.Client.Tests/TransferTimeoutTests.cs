using System.IO;
using System.Net;
using System.Net.Http;
using HwSync.Api.Client;
using HwSync.Client.Windows.Configuration;

namespace HwSync.Client.Tests
{
    /// <summary>Проверки ожидания прогресса передачи и отмены.</summary>
    public class TransferTimeoutTests
    {
        /// <summary>Проверяет продление ожидания при поступлении блоков файла.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task Progress_ExtendsTransferBeyondTimeout(bool upload)
        {
            using HttpClient ordinary = new(new TransferHandler())
            {
                Timeout = TimeSpan.FromMilliseconds(30)
            };
            using HttpClient transfers = new(new TransferHandler())
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            HwSyncApiClient api = new(ordinary, new("http://localhost"), transfers, TimeSpan.FromMilliseconds(500));
            await RunTransfer(api, upload, CancellationToken.None);
        }

        /// <summary>Проверяет остановку передачи без прогресса.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void StalledTransfer_TimesOut(bool upload)
        {
            using HttpClient ordinary = new(new TransferHandler());
            using HttpClient transfers = new(new TransferHandler())
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            HwSyncApiClient api = new(ordinary, new("http://localhost"), transfers, TimeSpan.FromMilliseconds(50));
            Assert.CatchAsync<OperationCanceledException>(async () => await RunTransfer(api, upload, CancellationToken.None));
        }

        /// <summary>Проверяет приоритет пользовательской отмены над тайм-аутом.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void UserCancellation_StopsTransferBeforeConfiguredTimeout(bool upload)
        {
            using HttpClient ordinary = new(new TransferHandler());
            using HttpClient transfers = new(new TransferHandler())
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            HwSyncApiClient api = new(ordinary, new("http://localhost"), transfers, TimeSpan.FromMinutes(30));
            using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(50));
            Assert.CatchAsync<OperationCanceledException>(async () => await RunTransfer(api, upload, cancellation.Token));
        }

        /// <summary>Проверяет чтение настроенного и стандартного тайм-аутов.</summary>
        [TestCase("{}", 1800)]
        [TestCase("{\"FileTransferTimeoutSeconds\":60}", 60)]
        public void Settings_ReadTimeout(string json, int expected)
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".json");
            File.WriteAllText(path, json);
            try
            {
                Assert.That(ClientSettingsReader.Load(path).FileTransferTimeoutSeconds, Is.EqualTo(expected));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Проверяет отказ при недопустимом интервале ожидания.</summary>
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(2147484)]
        public void Settings_RejectInvalidTimeout(int value)
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".json");
            File.WriteAllText(path, "{\"FileTransferTimeoutSeconds\":" + value + "}");
            try
            {
                Assert.Throws<InvalidDataException>(() => ClientSettingsReader.Load(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>Выполняет тестовую передачу в выбранном направлении.</summary>
        private static async Task RunTransfer(HwSyncApiClient api, bool upload, CancellationToken token)
        {
            using PacedStream source = new();
            using MemoryStream output = new();
            if (upload)
            {
                await api.UploadFileAsync(Guid.NewGuid(), "test.bin", source, token);
            }
            else
            {
                await api.DownloadFileAsync(Guid.NewGuid(), "test.bin", output, token);
                Assert.That(output.Length, Is.EqualTo(8));
            }
        }

        /// <summary>HTTP-обработчик с медленной передачей для проверки тайм-аутов.</summary>
        private sealed class TransferHandler : HttpMessageHandler
        {
            /// <summary>Имитирует приём загрузки или медленную выдачу файла.</summary>
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                if (request.Content is not null)
                {
                    await request.Content.CopyToAsync(Stream.Null, token);
                    return new(HttpStatusCode.NoContent);
                }
                return new(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new PacedStream())
                };
            }
        }

        /// <summary>Тестовый поток, выдающий данные небольшими блоками с задержкой.</summary>
        private sealed class PacedStream : MemoryStream
        {
            /// <summary>Создаёт небольшое содержимое для проверки длительной передачи.</summary>
            public PacedStream() : base(new byte[8])
            {

            }

            /// <summary>Выдаёт один байт после задержки с поддержкой отмены.</summary>
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                await Task.Delay(150, cancellationToken);
                return await base.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], cancellationToken);
            }

            /// <summary>Копирует тестовые данные блоками с управляемой задержкой.</summary>
            public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
            {
                byte[] buffer = new byte[1];
                int read;
                while ((read = await ReadAsync(buffer.AsMemory(), token)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), token);
                }
            }
        }
    }
}
