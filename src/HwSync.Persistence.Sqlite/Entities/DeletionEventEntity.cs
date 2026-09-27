namespace HwSync.Persistence.Sqlite.Entities
{
    /// <summary>
    /// Событие исчезновения ранее известного файла.
    /// </summary>
    public sealed class DeletionEventEntity
    {
        /// <summary>
        /// Последовательный номер события в базе участника.
        /// </summary>
        public long Number { get; set; }

        /// <summary>
        /// Идентификатор локальной папки.
        /// </summary>
        public string FolderId { get; set; } = "";

        /// <summary>
        /// Идентификатор участника, обнаружившего удаление.
        /// </summary>
        public string OriginParticipantId { get; set; } = "";

        /// <summary>
        /// Путь удалённого файла относительно корня папки.
        /// </summary>
        public string RelativePath { get; set; } = "";

        /// <summary>
        /// Время обнаружения удаления в UTC.
        /// </summary>
        public DateTimeOffset DeletedUtc { get; set; }

        /// <summary>
        /// Последний известный размер файла в байтах.
        /// </summary>
        public long PreviousSize { get; set; }

        /// <summary>
        /// Последнее известное время изменения файла в UTC.
        /// </summary>
        public DateTime PreviousModifiedUtc { get; set; }

        /// <summary>
        /// Признак действующего удаления; снимается при восстановлении файла.
        /// </summary>
        public bool Active { get; set; }
    }
}
