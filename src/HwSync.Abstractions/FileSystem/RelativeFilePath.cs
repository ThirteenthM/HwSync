namespace HwSync.Abstractions.FileSystem
{
    /// <summary>
    /// Сравнение относительных путей без учёта регистра и вида разделителя.
    /// </summary>
    public static class RelativeFilePath
    {
        /// <summary>
        /// Возвращает ключ пути с разделителем «/». Регистр сравнивает вызывающий код.
        /// </summary>
        public static string Key(string relativePath)
        {
            return relativePath.Replace('\\', '/');
        }

        /// <summary>
        /// Проверяет, что пути указывают на один файл.
        /// </summary>
        public static bool Same(string? left, string? right)
        {
            return left is not null
                && right is not null
                && StringComparer.OrdinalIgnoreCase.Equals(Key(left), Key(right));
        }
    }
}
