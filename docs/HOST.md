# HwSync.Host

Точка запуска HwSync на .NET 10. Регистрирует зависимости в DI, загружает настройки и управляет жизненным циклом фонового обработчика. REST API позволяет запускать сканирование в фоне. Сканирование по расписанию и применение изменений пока не подключены. См. [REST API](REST-API.md).

## Консоль

Из корня решения:

```powershell
dotnet run --project src/HwSync.Host -- --console
```

Остановка — Ctrl+C. Для запуска из Visual Studio выберите HwSync.Host стартовым проектом: профиль запуска уже содержит --console.

## Параметры командной строки

Разбор выполняет System.CommandLine 2.0.11. Параметры регистрозависимы.

- --console — консольный режим.
- --help — справка без запуска Host.
- --version — версия без запуска Host.
- --set Key=Value — переопределение настройки; параметр можно повторять.

```powershell
dotnet run --project src/HwSync.Host -- --help
dotnet run --project src/HwSync.Host -- --console --set Logging:LogLevel:Default=Debug
```

Значения с пробелами заключайте в кавычки: --set "Example=with spaces". Настройки передаются в Generic Host и имеют приоритет над JSON и переменными окружения. Ранее допустимую форму --Logging:LogLevel:Default Debug заменяет --set Logging:LogLevel:Default=Debug. Неизвестные параметры и некорректный --set завершают программу с ненулевым кодом без запуска Host.

## Служба Windows

Один и тот же exe поддерживает консоль и запуск через диспетчер служб Windows. Без --console окружение службы определяется автоматически; обычный запуск вне диспетчера служб тоже работает как консоль.

Пример публикации из корня решения:

```powershell
dotnet publish src/HwSync.Host -c Release -r win-x64 --self-contained true -o artifacts/HwSync.Host
```

Скопируйте публикацию в постоянную папку. Пример регистрации из PowerShell с правами администратора (замените путь на фактический):

```powershell
New-Service -Name HwSync -BinaryPathName '"C:\Services\HwSync\HwSync.Host.exe"' -DisplayName 'HwSync' -StartupType Manual
Start-Service HwSync
Stop-Service HwSync
```

При регистрации службы не указывайте --console. Выберите для службы учётную запись с доступом к будущим каталогам синхронизации. Регистрация службы не выполняется автоматически приложением.

## Настройки и зависимости

appsettings.json загружается из папки приложения, независимо от рабочего каталога. Поддерживаются стандартные настройки Generic Host: appsettings.{Environment}.json, переменные окружения и параметры командной строки. Например, Logging__LogLevel__Default задаёт уровень логирования через окружение.

Зарегистрированы IFileSnapshotProvider → DirectorySnapshotProvider, IChangeComparer → ChangeComparer, IChangeScanner → DirectoryChangeScanner. Сервисы создаются как transient; настройки будущих запусков не разделяются через singleton-экземпляры.

В консоли используются стандартные логи Generic Host; при запуске службой подключается Windows Event Log (его стандартный порог — Warning). Остановка службы и Ctrl+C передают отмену фоновому обработчику.
