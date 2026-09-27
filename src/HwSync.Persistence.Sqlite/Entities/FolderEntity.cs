namespace HwSync.Persistence.Sqlite.Entities
{
    /// <summary>
    /// Зарегистрированная папка участника.
    /// </summary>
    public sealed class FolderEntity
    {
        /// <summary>
        /// Постоянный идентификатор папки в локальной базе.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Нормализованный абсолютный путь к корню папки.
        /// </summary>
        public string RootPath { get; set; } = "";
    }
}
