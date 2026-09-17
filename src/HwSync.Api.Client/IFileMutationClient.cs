namespace HwSync.Api.Client
{
    /// <summary>Загрузка, удаление и проверка отсутствия серверных файлов.</summary>
    public interface IFileMutationClient
    {
        /// <summary>Передаёт отсутствующий на сервере файл без перезаписи.</summary>
        Task UploadFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token);

        /// <summary>Удаляет серверный файл из выбранного сравнения.</summary>
        Task DeleteServerFileAsync(Guid jobId, string relativePath, CancellationToken token);

        /// <summary>Проверяет отсутствие файла на сервере перед локальным удалением.</summary>
        Task EnsureServerFileMissingAsync(Guid jobId, string relativePath, CancellationToken token);
    }
}
