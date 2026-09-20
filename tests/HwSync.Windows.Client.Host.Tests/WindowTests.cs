using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Windows.Client.Host;
using HwSync.Windows.AppServices.Client.ViewModels;
using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.Client.Host.Tests
{
    /// <summary>
    /// Проверки размещения результатов в окне клиента.
    /// </summary>
    public class WindowTests
    {
        /// <summary>
        /// Проверяет построение окна с результатами сравнения.
        /// </summary>
        [Test]
        [Apartment(ApartmentState.STA)]
        public async Task Window_CanLayoutResults()
        {
            using MainViewModel model = new(address => new PreviewClient(), new StubSnapshotProvider(), new HwSync.Infrastructure.FileSystem.ComparedFileOperations(), new HwSync.Infrastructure.FileSystem.SourceFileReader(), new HwSync.Infrastructure.FileSystem.MissingFileSynchronizer(), new HwSync.Windows.AppServices.Client.SyncDecisionService(), new HwSync.Infrastructure.FileSystem.ConflictFileOperations(new HwSync.Infrastructure.FileSystem.SourceFileReader()))
            {
                ClientRootPath = @"D:\client-folder",
                RootPath = @"D:\Data\Documents"
            };
            model.LoadProfiles([new HwSync.Windows.Contract.Client.Configuration.SyncProfile
{
 Name = "Тестовый профиль", ServerRootPath = model.RootPath, ClientRootPath = model.ClientRootPath
}]);
            await model.StartCommand.ExecuteAsync(null);
            MainWindow window = new(model);
            try
            {
                FrameworkElement content = (FrameworkElement)window.Content;
                content.DataContext = model;
                content.Measure(new Size(1000, 800));
                content.Arrange(new Rect(0, 0, 1000, 800));
                content.UpdateLayout();
                Assert.That(content.ActualWidth, Is.EqualTo(1000 - content.Margin.Left - content.Margin.Right));
                Assert.That(model.Changes.Count, Is.EqualTo(3));
                string? output = Environment.GetEnvironmentVariable("HWSYNC_CLIENT_PREVIEW");
                if (output is not null)
                {
                    RenderTargetBitmap bitmap = new(1000, 800, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(content);
                    PngBitmapEncoder encoder = new();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using FileStream stream = File.Create(output);
                    encoder.Save(stream);
                }
            }
            finally
            {
                window.Close();
            }
        }

        /// <summary>
        /// Подставной API-клиент с данными для проверки окна.
        /// </summary>
        private sealed class PreviewClient : IHwSyncApiClient
        {
            private readonly Guid _id = Guid.NewGuid();

            /// <summary>
            /// Запрашивает готовность сервера.
            /// </summary>
            public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(new HealthResponse("ok"));

            /// <summary>
            /// Отправляет снимок клиента и запускает сравнение на сервере.
            /// </summary>
            public Task<ScanJobResponse> StartComparisonAsync(CompareFoldersRequest request, CancellationToken cancellationToken = default) => GetScanAsync(_id, cancellationToken);

            /// <summary>
            /// Запрашивает отмену задания на сервере.
            /// </summary>
            public Task<ScanJobResponse> CancelScanAsync(Guid id, CancellationToken cancellationToken = default) => GetScanAsync(id, cancellationToken);

            /// <summary>
            /// Получает состояние и результат задания.
            /// </summary>
            public Task<ScanJobResponse> GetScanAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(new ScanJobResponse(
                _id, ScanJobState.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                [new(FileChangeKind.Created, null, new("Documents/report.docx", 24576, DateTime.UtcNow)),
                 new(FileChangeKind.Created, null, new("Images/photo.jpg", 1048576, DateTime.UtcNow)),
                 new(FileChangeKind.Created, null, new("notes.txt", 512, DateTime.UtcNow))], null));
        }
    }
}
