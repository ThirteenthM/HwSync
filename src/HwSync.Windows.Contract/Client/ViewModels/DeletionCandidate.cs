namespace HwSync.Windows.Contract.Client.ViewModels
{
    /// <summary>
    /// Одна копия файла, предложенная к удалению.
    /// </summary>
    public sealed record DeletionCandidate(string Path, bool OnServer, long Size, DateTime ModifiedUtc)
    {
        public string Folder => Path.Replace('\\', '/').Contains('/') ? Path.Replace('\\', '/')[..Path.Replace('\\', '/').LastIndexOf('/')] : "";

        public string Name => Path.Replace('\\', '/')[(Path.Replace('\\', '/').LastIndexOf('/') + 1)..];

        public string Side => OnServer ? "Сервер" : "Клиент";

        public decimal SizeMegabytes => Size / 1_000_000m;
    }
}