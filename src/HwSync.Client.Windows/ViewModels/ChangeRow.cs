using HwSync.Api.Contracts;

namespace HwSync.Client.Windows.ViewModels
{
    public sealed record ChangeRow(string Kind, string Path, long? PreviousSize, long? CurrentSize)
    {
        public static ChangeRow FromDto(FileChangeDto change) => new(change.ChangeType switch
        {
            FileChangeKind.Created => "Только на сервере",
            FileChangeKind.Modified => "Отличается",
            FileChangeKind.Deleted => "Только на клиенте",
            _ => "Неизвестно"
        }, change.Current?.RelativePath ?? change.Previous?.RelativePath ?? "", change.Previous?.Size, change.Current?.Size);
    }
}
