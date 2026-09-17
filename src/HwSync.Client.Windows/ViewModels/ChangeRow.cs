using HwSync.Api.Contracts;

namespace HwSync.Client.Windows.ViewModels
{
    /// <summary>Строка различий для отображения в таблице клиента.</summary>
    public sealed record ChangeRow(string Kind, string Path, long? PreviousSize, long? CurrentSize)
    {
        /// <summary>Преобразует различие API в строку таблицы.</summary>
        public static ChangeRow FromDto(FileChangeDto change) => new(change.ChangeType switch
        {
            FileChangeKind.Created => "Только на сервере",
            FileChangeKind.Modified => "Отличается",
            FileChangeKind.Deleted => "Только на клиенте",
            _ => "Неизвестно"
        }, change.Current?.RelativePath ?? change.Previous?.RelativePath ?? "", change.Previous?.Size, change.Current?.Size);
    }
}
