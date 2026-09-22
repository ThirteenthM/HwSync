namespace HwSync.Windows.Contract.Client.ViewModels
{
    /// <summary>
    /// Папка с различиями в общем дереве сервера и клиента.
    /// </summary>
    public sealed record FolderNode(string Name, string RelativePath, IReadOnlyList<FolderNode> Children);
}