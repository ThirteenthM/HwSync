namespace HwSync.Abstractions.FileSystem
{
    /// <summary>
    /// Каталоги, которые серверу разрешено сравнивать и изменять.
    /// </summary>
    public interface IAllowedServerRoots
    {
        /// <summary>
        /// Отклоняет каталог вне настроенных корней.
        /// </summary>
        void EnsureAllowed(string rootPath);
    }
}
