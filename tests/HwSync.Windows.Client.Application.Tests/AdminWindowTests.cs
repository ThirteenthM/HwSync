using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using HwSync.Api.Client;
using HwSync.Api.Contracts.Administration;
using HwSync.Windows.AppServices.Administration;

namespace HwSync.Windows.Client.Application.Tests
{
    /// <summary>
    /// Проверяет построение отдельного административного окна.
    /// </summary>
    public class AdminWindowTests
    {
        /// <summary>
        /// Отображает настройки и историю без создания настоящего сервера.
        /// </summary>
        [Test]
        [Apartment(ApartmentState.STA)]
        public async Task AdminWindow_CanLayoutSettingsAndHistory()
        {
            using AdminViewModel model = new((_, _) => new PreviewApi()) { AccessToken = new string('t', 64) };
            await ((IAsyncRelayCommand)model.ConnectCommand).ExecuteAsync(null);
            model.SelectedFolder = model.Folders.Single();
            await ((IAsyncRelayCommand)model.LoadHistoryCommand).ExecuteAsync(null);
            HwSync.Windows.Admin.Application.MainWindow window = new(model);
            try
            {
                Grid content = (Grid)window.Content;
                content.DataContext = model;
                TabControl tabs = content.Children.OfType<TabControl>().Single();
                tabs.SelectedIndex = 1;
                content.Measure(new Size(1200, 850));
                content.Arrange(new Rect(0, 0, 1200, 850));
                content.UpdateLayout();
                Assert.That(model.Error, Is.Null);
                Assert.That(model.Deletions, Has.Count.EqualTo(2));
                Assert.That(content.ActualHeight, Is.GreaterThan(700));
                string? output = Environment.GetEnvironmentVariable("HWSYNC_ADMIN_PREVIEW");
                if (output is not null)
                {
                    RenderTargetBitmap bitmap = new(1200, 850, 96, 96, PixelFormats.Pbgra32);
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
        /// Пример ответов API для проверки размещения элементов.
        /// </summary>
        private sealed class PreviewApi : IAdministrationApiClient
        {
            /// <summary>
            /// Возвращает настройки тестового сервера.
            /// </summary>
            public Task<ServerSettingsDto> GetSettingsAsync(CancellationToken cancellationToken) =>
                Task.FromResult(new ServerSettingsDto("1.0", "SERVER", @"C:\HwSync\state.db",
                    ["http://localhost:5080"], "Information", true, "administrator"));

            /// <summary>
            /// Возвращает папку последнего снимка.
            /// </summary>
            public Task<IReadOnlyList<ServerFolderDto>> GetFoldersAsync(CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<ServerFolderDto>>([new("folder", @"E:\HwSync\Server", 42, 1)]);

            /// <summary>
            /// Возвращает активную и неактивную отметки удаления.
            /// </summary>
            public Task<DeletionPageDto> GetDeletionsAsync(string folderId, long after, CancellationToken cancellationToken) =>
                Task.FromResult(new DeletionPageDto([
                    new(1, "Documents/report.docx", "server-001", DateTimeOffset.UtcNow, 1234567, DateTimeOffset.UtcNow, true),
                    new(2, "Images/photo.jpg", "server-001", DateTimeOffset.UtcNow, 3456789, DateTimeOffset.UtcNow, false)
                ], null));

            /// <summary>
            /// Завершает тестовое подключение без внешних ресурсов.
            /// </summary>
            public void Dispose()
            {
            }
        }
    }
}
