using System.IO;
using System.Text.Json;

namespace HwSync.Client.Windows.Configuration
{
    public static class ClientSettingsReader
    {
        public static ClientSettings Load(string filePath)
        {
            if (!File.Exists(filePath)) { return new(); }

            using FileStream stream = File.OpenRead(filePath);
            ClientSettings settings = JsonSerializer.Deserialize<ClientSettings>(stream, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? throw new InvalidDataException("Конфигурация клиента не должна быть null.");

            if (!Uri.TryCreate(settings.ServerAddress, UriKind.Absolute, out Uri? address)
                || address.Scheme is not ("http" or "https"))
            {
                throw new InvalidDataException("ServerAddress должен быть HTTP(S)-адресом сервера.");
            }
            ValidatePath(settings.ServerRootPath, nameof(settings.ServerRootPath));
            ValidatePath(settings.ClientRootPath, nameof(settings.ClientRootPath));
            return settings;
        }

        private static void ValidatePath(string? path, string name)
        {
            if (path is null || (path.Length > 0 && !Path.IsPathFullyQualified(path)))
            {
                throw new InvalidDataException($"{name} должен быть абсолютным путём или пустой строкой.");
            }
        }
    }
}
