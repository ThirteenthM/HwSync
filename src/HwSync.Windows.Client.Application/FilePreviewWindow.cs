using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HwSync.Windows.Contract.Client.ViewModels;

namespace HwSync.Windows.Client.Application
{
    /// <summary>
    /// Просмотр текста, изображения или атрибутов одной копии.
    /// </summary>
    public sealed class FilePreviewWindow : Window
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly ContentControl _content = new();
        private readonly IMainViewModel _model;
        private readonly DeletionCandidate _file;

        /// <summary>
        /// Создаёт окно просмотра без запуска внешних программ.
        /// </summary>
        public FilePreviewWindow(IMainViewModel model, DeletionCandidate file)
        {
            _model = model;
            _file = file;
            Title = $"Просмотр — {file.Side} — {file.Name}";
            Width = 900;
            Height = 650;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DockPanel panel = new() { Margin = new Thickness(16) };
            TextBlock attributes = new()
            {
                Text = $"{file.Path}\n{file.Side} · {file.ModifiedUtc:yyyy-MM-dd HH:mm:ss} UTC · {file.SizeMegabytes:0.000} МБ",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            DockPanel.SetDock(attributes, Dock.Top);
            panel.Children.Add(attributes);
            Button close = new() { Content = "Закрыть", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(16, 8, 16, 8) };
            DockPanel.SetDock(close, Dock.Bottom);
            panel.Children.Add(close);
            panel.Children.Add(_content);
            Content = panel;
            Loaded += LoadPreview;
            Closed += (_, _) =>
            {
                _cancellation.Cancel();
                _cancellation.Dispose();
            };
        }

        /// <summary>
        /// Загружает поддерживаемое содержимое и показывает ошибки внутри окна.
        /// </summary>
        private async void LoadPreview(object sender, RoutedEventArgs e)
        {
            Loaded -= LoadPreview;
            CancellationToken token = _cancellation.Token;
            string extension = Path.GetExtension(_file.Name).ToLowerInvariant();
            bool image = extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif";
            bool text = extension is ".txt" or ".md" or ".json" or ".xml" or ".csv" or ".log" or ".cs" or ".xaml" or ".yaml" or ".yml" or ".ini" or ".config";
            _content.Content = new TextBlock { Text = "Загрузка…", TextWrapping = TextWrapping.Wrap };
            try
            {
                if (!image && !text)
                {
                    _content.Content = new TextBlock { Text = "Для этого формата доступны только атрибуты.", TextWrapping = TextWrapping.Wrap };
                    return;
                }

                byte[] bytes = await _model.LoadDeletionPreviewAsync(_file, token);
                token.ThrowIfCancellationRequested();
                using MemoryStream stream = new(bytes);
                if (image)
                {
                    BitmapImage bitmap = new();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 1600;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    _content.Content = new ScrollViewer
                    {
                        Content = new Image { Source = bitmap, Stretch = Stretch.Uniform },
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    };
                }
                else
                {
                    using StreamReader reader = new(stream, detectEncodingFromByteOrderMarks: true);
                    _content.Content = new TextBox
                    {
                        Text = await reader.ReadToEndAsync(token),
                        IsReadOnly = true,
                        FontFamily = new FontFamily("Consolas"),
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    };
                }
            }
            catch (OperationCanceledException)
            {
                _content.Content = new TextBlock { Text = "Просмотр отменён." };
            }
            catch (Exception exception)
            {
                _content.Content = new TextBlock { Text = exception.Message, TextWrapping = TextWrapping.Wrap };
            }
        }
    }
}