namespace HwSync.Abstractions.Models
{
    /// <summary>
    /// Предыдущее и текущее состояние изменившегося файла.
    /// </summary>
    public sealed record FileChange(
        FileChangeType ChangeType,
        FileSnapshot? Previous,
        FileSnapshot? Current
    );
}
