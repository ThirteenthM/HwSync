using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HwSync.Windows.Contract.Client.ViewModels;
using HwSync.Windows.Client.Application;

namespace HwSync.Windows.Client.Application.Tests
{
    /// <summary>
    /// Проверка отображения окна выбора решения конфликта.
    /// </summary>
    public sealed class ConflictWindowTests
    {
        /// <summary>
        /// Показывает атрибуты обеих версий и варианты отложенного действия.
        /// </summary>
        [TestCase(FileConflictKind.Content)]
        [TestCase(FileConflictKind.DeletedOnClient)]
        [TestCase(FileConflictKind.DeletedOnServer)]
        [Apartment(ApartmentState.STA)]
        public void ConflictWindow_LayoutsAttributesAndChoices(FileConflictKind kind)
        {
            ChangeRow row = new("Отличается", "Documents/report.txt", kind == FileConflictKind.DeletedOnClient ? null : 1250000, kind == FileConflictKind.DeletedOnServer ? null : 2450000)
            {
                IsConflict = true, ConflictKind = kind, Action = FileSyncDecision.KeepBoth,
                DeletedVersionSize = 1000000, DeletedVersionModifiedUtc = DateTime.UnixEpoch,
                PreviousModifiedUtc = new DateTime(2026, 9, 19, 9, 0, 0, DateTimeKind.Utc),
                CurrentModifiedUtc = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc)
            };
            ConflictWindow window = new(row);
            try
            {
                FrameworkElement content = (FrameworkElement)window.Content;
                content.DataContext = row;
                content.Measure(new Size(700, 540));
                content.Arrange(new Rect(0, 0, 700, 540));
                content.UpdateLayout();
                Assert.That(window.DataContext, Is.SameAs(row));
                Assert.That(window.Decision, Is.EqualTo(FileSyncDecision.KeepBoth));
                string? path = Environment.GetEnvironmentVariable("HWSYNC_CONFLICT_PREVIEW");
                if (path is not null)
                {
                    RenderTargetBitmap bitmap = new(700, 540, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(content);
                    PngBitmapEncoder encoder = new();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using FileStream output = File.Create(path);
                    encoder.Save(output);
                }
            }
            finally
            {
                window.Close();
            }
        }
    }
}
