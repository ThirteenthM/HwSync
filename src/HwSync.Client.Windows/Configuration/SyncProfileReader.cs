using System.IO;
using System.Text.Json;
namespace HwSync.Client.Windows.Configuration
{
    public static class SyncProfileReader
    {
        public static IReadOnlyList<SyncProfile> Load(string path, ClientSettings fallback)
        {
            if (!File.Exists(path)) { return [new() { ServerAddress = fallback.ServerAddress, ServerRootPath = fallback.ServerRootPath, ClientRootPath = fallback.ClientRootPath }]; }
            SyncProfile[] profiles = JsonSerializer.Deserialize<SyncProfile[]>(File.ReadAllText(path), new JsonSerializerOptions
            { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })
                ?? throw new InvalidDataException("Список профилей пуст.");
            HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
            if (profiles.Length == 0) { throw new InvalidDataException("Нужен хотя бы один профиль."); }
            foreach (SyncProfile profile in profiles)
            {
                if (profile is null || string.IsNullOrWhiteSpace(profile.Id) || !ids.Add(profile.Id)
                    || string.IsNullOrWhiteSpace(profile.Name) || !Uri.TryCreate(profile.ServerAddress, UriKind.Absolute, out Uri? address)
                    || address.Scheme is not ("http" or "https") || !Path.IsPathFullyQualified(profile.ClientRootPath)
                    || !Path.IsPathFullyQualified(profile.ServerRootPath))
                { throw new InvalidDataException("Профиль должен иметь уникальный Id, имя, адрес сервера и два абсолютных пути."); }
                if (profile.Rules is null || profile.Rules.MissingOnClient != "Copy" || profile.Rules.DifferentFiles != "Skip"
                    || profile.Rules.ClientOnlyFiles != "Keep" || profile.Rules.ServerDeletions != "RecordOnly")
                { throw new InvalidDataException("Пока поддерживаются правила Copy / Skip / Keep / RecordOnly. Неподдерживаемая стратегия не применяется молча."); }
            }
            return profiles;
        }
    }
}
