namespace HwSync.Windows.Contract.Administration
{
    /// <summary>
    /// Настройки подключения утилиты без сохранения ключа пользователя.
    /// </summary>
    public sealed class AdminSettings
    {
        public string ServerAddress { get; init; } = "http://localhost:5080/";
        public int RequestTimeoutSeconds { get; init; } = 30;
    }
}
