using System.Text.Json;

namespace HwSync.Client.Windows.Contract.Services
{
    /// <summary>
    /// Чтение типизированной конфигурации клиента.
    /// </summary>
    public interface IConfigurationReader<T> where T : class
    {
        /// <summary>
        /// Читает настройки или создаёт начальные при отсутствии файла.
        /// </summary>
        T Load(string filePath, JsonSerializerOptions? options = null);
    }
}
