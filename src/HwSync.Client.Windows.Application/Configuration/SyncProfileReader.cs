using HwSync.Client.Windows.Contract.Configuration;
using System.IO;

namespace HwSync.Client.Windows.Application.Configuration
{
    /// <summary>
    /// Проверка профилей папок и поддерживаемых правил.
    /// </summary>
    public sealed class SyncProfileReader : JsonConfigurationReader<List<SyncProfile>>
    {
        private readonly ClientSettings _fallback;

        /// <summary>
        /// Принимает настройки для профиля при отсутствии файла.
        /// </summary>
        public SyncProfileReader(ClientSettings fallback)
        {
            ArgumentNullException.ThrowIfNull(fallback);
            _fallback = fallback;
        }

        protected override string NullValueMessage => "Список профилей пуст.";

        /// <summary>
        /// Создаёт начальный профиль из общих настроек клиента.
        /// </summary>
        protected override List<SyncProfile> CreateDefault() =>
        [
            new()
            {
                ServerAddress = _fallback.ServerAddress,
                ServerRootPath = _fallback.ServerRootPath,
                ClientRootPath = _fallback.ClientRootPath
            }
        ];

        /// <summary>
        /// Проверяет идентификаторы, пути и правила каждого профиля.
        /// </summary>
        protected override void Validate(List<SyncProfile> profiles)
        {
            HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
            if (profiles.Count == 0)
            {
                throw new InvalidDataException("Нужен хотя бы один профиль.");
            }

            foreach (SyncProfile profile in profiles)
            {
                if (profile is null
                    || string.IsNullOrWhiteSpace(profile.Id)
                    || !ids.Add(profile.Id)
                    || string.IsNullOrWhiteSpace(profile.Name) || !Uri.TryCreate(profile.ServerAddress, UriKind.Absolute, out Uri? address)
                    || address.Scheme is not ("http" or "https") || !Path.IsPathFullyQualified(profile.ClientRootPath)
                    || !Path.IsPathFullyQualified(profile.ServerRootPath))
                {
                    throw new InvalidDataException("Профиль должен иметь уникальный Id, имя, адрес сервера и два абсолютных пути.");
                }

                if (profile.Reconciliation is null)
                {
                    throw new InvalidDataException("Не заданы правила согласования.");
                }

                if (profile.Rules is null || profile.Rules.MissingOnClient is not (SyncRule.Copy or SyncRule.Keep or SyncRule.Skip or SyncRule.Delete) || profile.Rules.DifferentFiles is not (SyncRule.Skip or SyncRule.Keep)
                    || profile.Rules.ClientOnlyFiles is not (SyncRule.Copy or SyncRule.Keep or SyncRule.Skip or SyncRule.Delete) || profile.Rules.ServerDeletions != SyncRule.RecordOnly)
                {
                    throw new InvalidDataException("Недопустимое правило профиля: односторонние файлы — Copy / Skip / Keep / Delete, разные файлы — Skip / Keep, история удалений — RecordOnly.");
                }
            }
        }
    }
}
