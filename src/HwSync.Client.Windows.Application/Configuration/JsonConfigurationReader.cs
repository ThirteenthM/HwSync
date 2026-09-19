using HwSync.Client.Windows.Contract.Configuration;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HwSync.Client.Windows.Application.Configuration
{
    /// <summary>
    /// Общее чтение JSON-конфигурации с проверкой в наследнике.
    /// </summary>
    public abstract class JsonConfigurationReader<T> : HwSync.Client.Windows.Contract.Services.IConfigurationReader<T> where T : class, new()
    {
        /// <summary>
        /// Создаёт общие параметры чтения JSON и текстовых перечислений.
        /// </summary>
        public static JsonSerializerOptions GetOptions() => new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
        };

        /// <summary>
        /// Читает и проверяет файл либо возвращает начальные настройки при его отсутствии.
        /// </summary>
        public T Load(string filePath, JsonSerializerOptions? options = null)
        {
            if (!File.Exists(filePath))
            {
                return CreateDefault();
            }

            using FileStream stream = File.OpenRead(filePath);
            T settings = JsonSerializer.Deserialize<T>(stream, options ?? GetOptions())
                ?? throw new InvalidDataException(NullValueMessage);
            Validate(settings);
            return settings;
        }

        protected virtual string NullValueMessage => "Конфигурация не должна быть null.";

        /// <summary>
        /// Создаёт начальные настройки для отсутствующего файла.
        /// </summary>
        protected virtual T CreateDefault() => new();

        /// <summary>
        /// Проверяет прочитанную конфигурацию по правилам наследника.
        /// </summary>
        protected virtual void Validate(T settings)
        {
        }
    }
}
