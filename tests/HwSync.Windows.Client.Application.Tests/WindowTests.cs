using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Windows.Client.Application;
using HwSync.Windows.AppServices.Client.ViewModels;
using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.Client.Application.Tests
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
                content.Measure(new Size(1400, 900));
                content.Arrange(new Rect(0, 0, 1400, 900));
                content.UpdateLayout();
                Assert.That(content.ActualWidth, Is.EqualTo(1400 - content.Margin.Left - content.Margin.Right));
                Assert.That(model.Changes, Has.Count.EqualTo(3));
                Button decision = Descendants(content).OfType<Button>().First(button => button.ContextMenu is not null && button.DataContext is ChangeRow);
                ContextMenu menu = decision.ContextMenu!;
                decision.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                try
                {
                    menu.ApplyTemplate();
                    menu.Measure(new Size(500, 500));
                    menu.Arrange(new Rect(0, 0, 500, 500));
                    menu.UpdateLayout();
                    Assert.That(menu.Items.Count, Is.GreaterThan(0));
                    MenuItem skip = Enumerable.Range(0, menu.Items.Count)
                        .Select(index => (MenuItem)menu.ItemContainerGenerator.ContainerFromIndex(index))
                        .Single(item => item.CommandParameter is FileDecisionChoice choice && choice.Action == FileSyncDecision.Skip);
                    Assert.That(skip.Command, Is.SameAs(model.SetFileDecisionCommand));
                    Assert.That(skip.Command.CanExecute(skip.CommandParameter), Is.True);
                    skip.Command.Execute(skip.CommandParameter);
                    Assert.That(model.Changes.Single(row => row.Path == ((FileDecisionChoice)skip.CommandParameter).Path).IsManualDecision, Is.True);
                }
                finally
                {
                    menu.IsOpen = false;
                }
                content.UpdateLayout();
                string? output = Environment.GetEnvironmentVariable("HWSYNC_CLIENT_PREVIEW");
                if (output is not null)
                {
                    RenderTargetBitmap bitmap = new(1400, 900, 96, 96, PixelFormats.Pbgra32);
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
        /// Обходит визуальные элементы для проверки привязок формы.
        /// </summary>
        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                yield return child;
                foreach (DependencyObject descendant in Descendants(child))
                {
                    yield return descendant;
                }
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
