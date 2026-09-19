using HwSync.Api.Contracts;
using HwSync.Client.Windows.Contract.ViewModels;

namespace HwSync.Client.Windows.Application.ViewModels
{
    /// <summary>
    /// Преобразование ответа API в данные строки таблицы.
    /// </summary>
    internal static class ChangeRowMapper
    {
        /// <summary>
        /// Создаёт строку по различию файлов из ответа сервера.
        /// </summary>
        public static ChangeRow FromDto(FileChangeDto change) => new(change.ChangeType switch
        {
            FileChangeKind.Created => "Только на сервере",
            FileChangeKind.Modified => "Отличается",
            FileChangeKind.Deleted => "Только на клиенте",
            _ => "Неизвестно"
        }, change.Current?.RelativePath ?? change.Previous?.RelativePath ?? "", change.Previous?.Size, change.Current?.Size);
    }
}
