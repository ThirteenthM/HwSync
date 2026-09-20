using System.Text.Json;

namespace HwSync.Windows.Contract.Common.Services
{
    /// <summary>
    /// Чтение типизированной конфигурации приложения.
    /// </summary>
    public interface IConfigurationReader<T> where T : class
    {
        /// <summary>
        /// Читает настройки или создаёт начальные при отсутствии файла.
        /// </summary>
        T Load(string filePath, JsonSerializerOptions? options = null);
    }
}
