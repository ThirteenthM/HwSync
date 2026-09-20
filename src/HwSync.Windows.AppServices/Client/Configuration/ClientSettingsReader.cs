using HwSync.Windows.AppServices.Common.Configuration;
using HwSync.Windows.Contract.Client.Configuration;
using System.IO;

namespace HwSync.Windows.AppServices.Client.Configuration
{
    /// <summary>
    /// Проверка общих настроек клиента.
    /// </summary>
    public sealed class ClientSettingsReader : JsonConfigurationReader<ClientSettings>
    {
        protected override string NullValueMessage => "Конфигурация клиента не должна быть null.";


        /// <summary>
        /// Проверяет адрес сервера, тайм-аут и абсолютные пути.
        /// </summary>
        protected override void Validate(ClientSettings settings)
        {
            if (!Uri.TryCreate(settings.ServerAddress, UriKind.Absolute, out Uri? address) || address.Scheme is not ("http" or "https"))
            {
                throw new InvalidDataException("ServerAddress должен быть HTTP(S)-адресом сервера.");
            }
            if (settings.FileTransferTimeoutSeconds is < 1 or > 2147483)
            {
                throw new InvalidDataException("FileTransferTimeoutSeconds должен быть от 1 до 2147483 секунд.");
            }
            ValidatePath(settings.DatabasePath, nameof(settings.DatabasePath));
            ValidatePath(settings.ServerRootPath, nameof(settings.ServerRootPath));
            ValidatePath(settings.ClientRootPath, nameof(settings.ClientRootPath));
        }

        /// <summary>
        /// Проверяет, что путь абсолютный или пустой.
        /// </summary>
        private static void ValidatePath(string? path, string name)
        {
            if (path is null || (path.Length > 0 && !Path.IsPathFullyQualified(path)))
            {
                throw new InvalidDataException($"{name} должен быть абсолютным путём или пустой строкой.");
            }
        }
    }
}
