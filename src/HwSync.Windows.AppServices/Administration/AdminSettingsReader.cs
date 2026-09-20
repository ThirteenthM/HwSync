using HwSync.Windows.AppServices.Common.Configuration;
using HwSync.Windows.Contract.Administration;

namespace HwSync.Windows.AppServices.Administration
{
    /// <summary>
    /// Проверяет настройки административного приложения.
    /// </summary>
    public sealed class AdminSettingsReader : JsonConfigurationReader<AdminSettings>
    {
        /// <summary>
        /// Проверяет адрес сервера и допустимый тайм-аут запросов.
        /// </summary>
        protected override void Validate(AdminSettings settings)
        {
            if (!Uri.TryCreate(settings.ServerAddress, UriKind.Absolute, out Uri? address)
                || address.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(address.UserInfo)
                || !string.IsNullOrEmpty(address.Query) || !string.IsNullOrEmpty(address.Fragment))
            {
                throw new InvalidDataException("ServerAddress должен быть HTTP(S)-адресом без пароля, query и fragment.");
            }

            if (settings.RequestTimeoutSeconds is < 1 or > 3600)
            {
                throw new InvalidDataException("RequestTimeoutSeconds должен быть от 1 до 3600 секунд.");
            }
        }
    }
}
