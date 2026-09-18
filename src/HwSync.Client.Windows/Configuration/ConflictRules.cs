using HwSync.Abstractions.Models;

namespace HwSync.Client.Windows.Configuration
{
    /// <summary>
    /// Правила текущего режима копирования отсутствующих файлов.
    /// </summary>
    public sealed class ConflictRules
    {
        public string MissingOnClient
        {
            get;
            init;
        } = "Copy";
        public string DifferentFiles
        {
            get;
            init;
        } = "Skip";
        public string ClientOnlyFiles
        {
            get;
            init;
        } = "Keep";
        public string ServerDeletions
        {
            get;
            init;
        } = "RecordOnly";
    }
}
