namespace HwSync.Persistence.Sqlite.Entities
{
    /// <summary>
    /// Подтверждённое состояние пары клиент-папка.
    /// </summary>
    public sealed class AcknowledgedStateEntity
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
        /// Подтверждённые версии файлов папки.
        /// </summary>
        public List<AcknowledgedFileEntity> Files { get; set; } = [];
    }
}
