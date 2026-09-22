namespace HwSync.Api.Handlers
{
    /// <summary>
    /// Серверное действие над файлом из сравнения.
    /// </summary>
    public enum FileMutationOperation
    {
        Upload, Delete, VerifyMissing, DeleteCompared, VerifyUnchanged
    }
}
