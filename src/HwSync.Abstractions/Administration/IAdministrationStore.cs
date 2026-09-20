namespace HwSync.Abstractions.Administration
{
    /// <summary>
    /// Чтение каталога папок и истории без изменения состояния синхронизации.
    /// </summary>
    public interface IAdministrationStore
    {
        /// <summary>
        /// Возвращает папки, зарегистрированные успешным сканированием.
        /// </summary>
        IReadOnlyList<RegisteredFolder> GetFolders();

        /// <summary>
        /// Возвращает страницу журнала либо null для неизвестной папки.
        /// </summary>
        IReadOnlyList<DeletionEntry>? GetDeletions(string folderId, long after, int limit);
    }
}
