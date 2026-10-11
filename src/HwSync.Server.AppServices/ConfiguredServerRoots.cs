using HwSync.Abstractions.FileSystem;
using Microsoft.Extensions.Configuration;

namespace HwSync.Server.AppServices
{
    /// <summary>
    /// Список Storage:AllowedRoots: сам корень и каталоги внутри него.
    /// </summary>
    public sealed class ConfiguredServerRoots : IAllowedServerRoots
    {
        private readonly string[] _roots;

        /// <summary>
        /// Читает абсолютные корни. Пустой список запрещает все каталоги.
        /// </summary>
        public ConfiguredServerRoots(IConfiguration configuration)
        {
            string[] configured = configuration.GetSection("Storage:AllowedRoots").Get<string[]>() ?? [];
            List<string> roots = [];
            foreach (string root in configured)
            {
                if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
                {
                    throw new InvalidOperationException("Storage:AllowedRoots должен содержать только абсолютные пути.");
                }

                roots.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)));
            }

            _roots = roots.ToArray();
        }

        /// <summary>
        /// Проверяет, что каталог совпадает с разрешённым корнем или лежит внутри него.
        /// </summary>
        public void EnsureAllowed(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Path.IsPathFullyQualified(rootPath))
            {
                throw new ArgumentException("RootPath должен быть абсолютным путём.");
            }

            if (_roots.Length == 0)
            {
                throw new ArgumentException("Разрешённые каталоги сервера не заданы.");
            }

            string requested = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
            foreach (string allowed in _roots)
            {
                string prefix = allowed + Path.DirectorySeparatorChar;
                if (requested.Equals(allowed, StringComparison.OrdinalIgnoreCase)
                    || requested.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            throw new ArgumentException("Каталог не входит в разрешённые корни сервера.");
        }
    }
}
