namespace HwSync.Core.Tests.Helpers;

/// <summary>
/// Изолированная временная папка для файловых тестов.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public string Path
    {
        get;
    }

    /// <summary>
    /// Создаёт отдельную папку теста.
    /// </summary>
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            Guid.NewGuid().ToString());

        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// Удаляет временную папку теста.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
