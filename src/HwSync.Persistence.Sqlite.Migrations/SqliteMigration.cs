namespace HwSync.Persistence.Sqlite.Migrations
{
    /// <summary>
    /// Неизменяемое описание одного изменения схемы.
    /// </summary>
    public sealed record SqliteMigration(int Version, string Name, string Sql);
}
