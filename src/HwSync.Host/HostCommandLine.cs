using System.CommandLine;

namespace HwSync.Host
{
    /// <summary>Разбор режима запуска и параметров конфигурации Host.</summary>
    public static class HostCommandLine
    {
        /// <summary>Создаёт команду запуска с проверкой аргументов.</summary>
        public static RootCommand CreateCommand(Func<bool, string[], CancellationToken, Task> runHost)
        {
            Option<bool> consoleOption = new("--console")
            {
                Description = "Запустить в консольном режиме. Остановка — Ctrl+C."
            };
            Option<string[]> settingsOption = new("--set")
            {
                Description = "Настройка Host в формате Key=Value. Можно указать несколько раз.",
                Arity = ArgumentArity.OneOrMore,
                AllowMultipleArgumentsPerToken = false
            };
            settingsOption.Validators.Add(result =>
            {
                foreach (string setting in result.GetValueOrDefault<string[]>() ?? [])
                {
                    int separator = setting.IndexOf('=');
                    if (separator <= 0 || string.IsNullOrWhiteSpace(setting[..separator])
                        || setting.StartsWith('-') || setting.StartsWith('/'))
                    {
                        result.AddError("Параметр --set должен иметь формат Key=Value с непустым ключом без префикса -- или /.");
                    }
                }
            });

            RootCommand command = new("HwSync — запуск консоли или службы Windows.");
            command.Options.Add(consoleOption);
            command.Options.Add(settingsOption);
            command.SetAction(async (result, cancellationToken) =>
            {
                await runHost(result.GetValue(consoleOption),
                    result.GetValue(settingsOption) ?? [], cancellationToken);
            });
            return command;
        }
    }
}
