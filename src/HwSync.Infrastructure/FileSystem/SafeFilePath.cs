namespace HwSync.Infrastructure.FileSystem
{
    /// <summary>
    /// Проверка относительных путей файлов синхронизации.
    /// </summary>
    public static class SafeFilePath
    {
        /// <summary>
        /// Разрешает путь внутри корня, отклоняя обход каталога и ссылки внутри этого корня.
        /// </summary>
        public static string Resolve(string rootPath, string relativePath)
        {
            string relative = relativePath.Replace('\\', '/');
            if (!Path.IsPathFullyQualified(rootPath) || string.IsNullOrWhiteSpace(relative) || relative.StartsWith('/')
                || relative.Contains(':') || relative.Split('/').Any(part => part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ')))
            {
                throw new IOException("Недопустимый относительный путь файла.");
            }
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
            string result = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = root + Path.DirectorySeparatorChar;
            if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Путь выходит за пределы папки.");
            }

            for (string? current = result; current is not null; current = Path.GetDirectoryName(current))
            {
                bool isRoot = current.Equals(root, StringComparison.OrdinalIgnoreCase);
                if (!isRoot && !current.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if ((File.Exists(current) || Directory.Exists(current))
                    && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Ссылки и junction в путях синхронизации пока не поддерживаются.");
                }

                if (isRoot)
                {
                    break;
                }
            }

            return result;
        }
    }
}
