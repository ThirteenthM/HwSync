using HwSync.Api.Contracts;

namespace HwSync.Api.Client
{
    /// <summary>
    /// Чтение серверной истории удалений для завершённого сравнения.
    /// </summary>
    public interface IDeletionHistoryClient
    {
        /// <summary>
        /// Возвращает отметки удаления вместе с последней известной версией.
        /// </summary>
        Task<IReadOnlyList<DeletedFileDto>> GetDeletedFilesAsync(Guid jobId, CancellationToken token);
    }
}
