namespace HwSync.Persistence.Sqlite.Entities
{
    /// <summary>
    /// Атрибуты файла последнего успешного сканирования.
    /// </summary>
    public sealed class FileSnapshotEntity
    {
        /// <summary>
        /// Идентификатор локальной папки.
        /// </summary>
        public string FolderId { get; set; } = "";

        /// <summary>
        /// Путь файла относительно корня папки.
        /// </summary>
        public string RelativePath { get; set; } = "";

        /// <summary>
        /// Размер файла в байтах.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Время последнего изменения файла в UTC.
        /// </summary>
        public DateTime ModifiedUtc { get; set; }
    }
}
