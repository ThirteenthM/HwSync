namespace HwSync.Abstractions.Models
{
    /// <summary>
    /// Одинаковое имя сохраняемой клиентской версии на обоих участниках.
    /// </summary>
    public static class ConflictCopyPath
    {
        /// <summary>
        /// Формирует имя копии, уникальное для задания сравнения.
        /// </summary>
        public static string Create(string relativePath, Guid comparisonId)
        {
            string extension = Path.GetExtension(relativePath);
            return relativePath[..^extension.Length] + $" (client-conflict-{comparisonId:N})" + extension;
        }
    }
}
