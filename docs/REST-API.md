# REST API HwSync

Host запускается как консоль или служба Windows и обслуживает один и тот же HTTP API. Сейчас доступ разрешён только локальным подключениям, без аутентификации. По умолчанию адрес http://localhost:5080 задан параметром Urls в appsettings.json. Сетевой доступ — следующий этап: HTTPS, аутентификация и права на задания должны появиться до снятия ограничения на локальные подключения. Изменение адреса само по себе не снимает ограничение (удалённые подключения получают 403).

## Запуск

Из корня решения:

```powershell
dotnet run --project src/HwSync.Host -- --console
```

Изменение локального порта:

```powershell
dotnet run --project src/HwSync.Host -- --console --set Urls=http://localhost:5081
```

## Операции

- GET /health — проверка доступности, 200.
- POST /api/v1/scan-jobs — поставить сканирование в очередь, 202 и заголовок Location.
- GET /api/v1/scan-jobs/{id} — состояние и результат, 200; неизвестный или вытесненный идентификатор — 404.
- POST /api/v1/scan-jobs/{id}/cancel — запрос отмены, 200; неизвестный идентификатор — 404. Повторная отмена безопасна, завершённое задание не меняется.

Некорректные параметры дают 400; заполненная очередь или остановка обработчика — 409. JSON использует camelCase; состояния и типы изменений передаются строками.

Пример в PowerShell (замените путь на каталог, который нужно просканировать):

```powershell
$body = @{
    rootPath = 'D:\#Homework\HwSync'
    previousSnapshot = @()
} | ConvertTo-Json -Depth 10
$job = Invoke-RestMethod -Method Post -Uri 'http://localhost:5080/api/v1/scan-jobs' -ContentType 'application/json' -Body $body
Invoke-RestMethod -Uri "http://localhost:5080/api/v1/scan-jobs/$($job.id)"
# При необходимости запросить отмену:
Invoke-RestMethod -Method Post -Uri "http://localhost:5080/api/v1/scan-jobs/$($job.id)/cancel"
```

RootPath — абсолютный путь на компьютере сервера, а не на компьютере будущего сетевого клиента. PreviousSnapshot — обязательный массив прежних FileSnapshot (relativePath, size, lastWriteTimeUtc). Пустой массив означает, что все найденные файлы будут отмечены Created. Сервер не сохраняет предыдущий снимок автоматически.

## Выполнение и ограничения

Состояния: Queued → Running → Completed / Failed. Отмена ожидающего задания сразу даёт Cancelled; для выполняющегося сначала возвращается CancellationRequested, затем Cancelled. У текущего синхронного сканера нет CancellationToken: чтение каталога не прерывается посреди операции, после его завершения результат отменённого задания отбрасывается. Остановка Host запрашивает отмену всех незавершённых заданий.

Результат находится в changes завершённого задания: тип изменения, предыдущие и текущие атрибуты файла. Ошибка находится в error; подробное исключение пишется в журнал сервера. Следующее задание выполняется даже после ошибки предыдущего.

Выполняется одно задание за раз. Очередь ограничена 100 элементами; хранится до 100 состояний, старые завершённые задания вытесняются при новых запусках. Результаты хранятся в памяти и могут занимать значительный объём для больших каталогов. После перезапуска история и очередь теряются. Постраничная выдача и постоянное хранилище пока не реализованы.

Это API сканирования изменений, а не копирования: создание, изменение и удаление файлов на целевой стороне ещё не выполняются.

Контракт IScanJobService и внутренние модели находятся в Abstractions, очередь и управление состояниями — в Core. Контроллеры, обработчик запросов и преобразование DTO находятся в HwSync.Api. Host подключает API и запускает фоновый обработчик. Логика управления заданиями не зависит от HTTP.

## Проекты API и клиентский пакет

HwSync.Api.Contracts содержит только публичные типы HTTP: StartScanJobRequest, ScanJobResponse, FileSnapshotDto, FileChangeDto, FileChangeKind, ScanJobState и HealthResponse. Он не ссылается на ASP.NET Core, Abstractions, Core или Host. Внутренние модели не являются сетевыми контрактами: ScanJobHandler явно преобразует их в DTO.

HwSync.Api содержит Controllers и Handlers. ScanJobsController только делегирует запросы ScanJobHandler; обработчик вызывает IScanJobService и формирует HTTP-ответ. HealthController возвращает статический ответ доступности. AddHwSyncApi регистрирует обработчик, MVC-контроллеры и JSON-настройки; MapHwSyncApi подключает маршруты и ограничение локального доступа.

Host вызывает только методы подключения API. Маршруты v1 и JSON-имена полей сохранены. Клиент десериализует строки перечислений через System.Text.Json.Serialization.JsonStringEnumConverter с JsonSerializerDefaults.Web.

Создание локального пакета из корня решения:

```powershell
dotnet pack src/HwSync.Api.Contracts -c Release -o artifacts/packages
```

Пакет HwSync.Api.Contracts имеет начальную версию 0.1.0 и целевую платформу net10.0, как решение. Для клиента на более старой платформе нужно отдельно согласовать поддерживаемые target frameworks. Публикация в NuGet пока не настроена и не выполнялась. Ошибки API используют стандартный ProblemDetails, ошибки валидации — ValidationProblemDetails ASP.NET Core.
