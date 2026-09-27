namespace HwSync.Persistence.Sqlite.Entities
{
    /// <summary>
    /// Подтверждённая версия одного файла.
    /// </summary>
    public sealed class AcknowledgedFileEntity
    {
        /// <summary>
        /// Идентификатор клиента, подтвердившего состояние.
        /// </summary>
        public Guid ClientId { get; set; }

        /// <summary>
        /// Идентификатор папки подтверждённого состояния.
        /// </summary>
        public string FolderId { get; set; } = "";

        /// <summary>
        /// Путь файла относительно корня папки.
        /// </summary>
        public string RelativePath { get; set; } = "";

        /// <summary>
        /// Подтверждённый номер версии файла.
        /// </summary>
        public long Revision { get; set; }

        /// <summary>
        /// Хеш содержимого, если он был рассчитан.
        /// </summary>
        public string? ContentHash { get; set; }
    }
}
