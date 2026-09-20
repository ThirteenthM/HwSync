namespace HwSync.Api
{
    /// <summary>
    /// Фактический путь хранилища, предоставленный точкой сборки сервера.
    /// </summary>
    public sealed record ServerStorageInfo(string DatabasePath);
}
