namespace HwSync.Persistence.Sqlite.Entities
{
    /// <summary>
    /// Постоянный идентификатор владельца базы.
    /// </summary>
    public sealed class ParticipantEntity
    {
        /// <summary>
        /// Ключ единственной записи участника, всегда равный 1.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Постоянный идентификатор владельца базы.
        /// </summary>
        public string ParticipantId { get; set; } = "";
    }
}
