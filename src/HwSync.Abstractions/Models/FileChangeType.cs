namespace HwSync.Abstractions.Models
{
    /// <summary>
    /// Вид изменения файла относительно предыдущего снимка.
    /// </summary>
    public enum FileChangeType
    {
        Created,
        Modified,
        Deleted,
    }
}
