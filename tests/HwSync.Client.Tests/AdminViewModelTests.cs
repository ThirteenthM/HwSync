using CommunityToolkit.Mvvm.Input;
using HwSync.Api.Client;
using HwSync.Api.Contracts.Administration;
using HwSync.Windows.AppServices.Administration;
using HwSync.Windows.Contract.Administration;
using Microsoft.Extensions.DependencyInjection;

namespace HwSync.Client.Tests
{
    /// <summary>
    /// Проверки просмотра истории без зависимости от WPF.
    /// </summary>
    public class AdminViewModelTests
    {
        /// <summary>
        /// Подключение и страницы показывают данные; смена адреса убирает старое состояние.
        /// </summary>
        [Test]
        public async Task Model_ConnectsReadsPagesAndClearsOnAddressChange()
        {
            FakeApi api = new();
            using AdminViewModel model = CreateModel(api);
            await Execute(model.ConnectCommand);
            Assert.That(model.Error, Is.Null);
            Assert.That(model.Folders, Has.Count.EqualTo(1));
            model.SelectedFolder = model.Folders[0];
            await Execute(model.LoadHistoryCommand);
            Assert.That(model.Deletions.Single().SizeMb, Is.EqualTo(1.234567m));
            Assert.That(model.LoadMoreCommand.CanExecute(null), Is.True);
            await Execute(model.LoadMoreCommand);
            Assert.That(api.Cursors, Is.EqualTo(new long[] { 0, 1 }));
            Assert.That(model.Deletions, Has.Count.EqualTo(2));
            Assert.That(model.LoadMoreCommand.CanExecute(null), Is.False);
            model.ServerAddress = "http://localhost:5090";
            Assert.That(model.Settings, Is.Empty);
            Assert.That(model.Folders, Is.Empty);
            Assert.That(model.Deletions, Is.Empty);
            Assert.That(model.LoadHistoryCommand.CanExecute(null), Is.False);
            Assert.That(api.Disposed, Is.True);
        }

        /// <summary>
        /// Неверные настройки и отказ сервера становятся сообщением, а не падением окна.
        /// </summary>
        [Test]
        public async Task Model_ShowsValidationAndConnectionErrors()
        {
            FakeApi api = new() { Fail = true };
            using AdminViewModel model = CreateModel(api);
            model.AccessToken = "";
            await Execute(model.ConnectCommand);
            Assert.That(model.Error, Does.Contain("ключ"));
            model.AccessToken = new string('t', 64);
            await Execute(model.ConnectCommand);
            Assert.That(model.Error, Does.Contain("denied"));
            Assert.That(model.Settings, Is.Empty);
            Assert.That(model.Folders, Is.Empty);
            Assert.That(model.IsBusy, Is.False);
        }

        /// <summary>
        /// Отмена и смена папки не оставляют в интерфейсе запоздалый результат.
        /// </summary>
        [Test]
        public async Task Model_CancelsHistoryAndAllowsRetry()
        {
            FakeApi api = new();
            using AdminViewModel model = CreateModel(api);
            await Execute(model.ConnectCommand);
            model.SelectedFolder = model.Folders[0];
            api.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task request = Execute(model.LoadHistoryCommand);
            Assert.That(model.IsBusy, Is.True);
            model.CancelCommand.Execute(null);
            await request;
            Assert.That(model.Status, Does.Contain("отменён"));
            Assert.That(model.Deletions, Is.Empty);
            Assert.That(model.LoadHistoryCommand.CanExecute(null), Is.True);

            Task changed = Execute(model.LoadHistoryCommand);
            model.SelectedFolder = null;
            api.Pending.TrySetResult(new([new(1, "late.txt", "server", DateTimeOffset.UtcNow, 10, DateTimeOffset.UtcNow, true)], null));
            await changed;
            Assert.That(model.Deletions, Is.Empty);
            Assert.That(model.SelectedFolder, Is.Null);
        }

        /// <summary>
        /// DI утилиты не регистрирует участника синхронизации и его SQLite.
        /// </summary>
        [Test]
        public void AdminContainer_DoesNotRegisterSynchronizationServices()
        {
            IServiceCollection services = new ServiceCollection();
            services.AddWindowsAdminServices(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
            Assert.That(provider.GetRequiredService<IAdminViewModel>(), Is.TypeOf<AdminViewModel>());
            Assert.That(provider.GetService<HwSync.Windows.Contract.Client.ViewModels.IMainViewModel>(), Is.Null);
            Assert.That(provider.GetService<HwSync.Abstractions.FileSystem.IFolderHistory>(), Is.Null);
        }

        /// <summary>
        /// Создаёт модель с тестовым ключом и API.
        /// </summary>
        private static AdminViewModel CreateModel(FakeApi api) => new((_, _) => api)
        {
            AccessToken = new string('t', 64)
        };

        /// <summary>
        /// Ожидает завершения команды модели.
        /// </summary>
        private static Task Execute(System.Windows.Input.ICommand command) => ((IAsyncRelayCommand)command).ExecuteAsync(null);

        /// <summary>
        /// Управляемые ответы для проверки страниц, ошибок и отмены.
        /// </summary>
        private sealed class FakeApi : IAdministrationApiClient
        {
            public bool Fail { get; init; }
            public bool Disposed { get; private set; }
            public List<long> Cursors { get; } = [];
            public TaskCompletionSource<DeletionPageDto>? Pending { get; set; }

            /// <summary>
            /// Возвращает настройки или имитирует отказ доступа.
            /// </summary>
            public Task<ServerSettingsDto> GetSettingsAsync(CancellationToken cancellationToken)
            {
                if (Fail)
                {
                    throw new HwSyncApiException(System.Net.HttpStatusCode.Forbidden, "denied");
                }

                return Task.FromResult(new ServerSettingsDto("1", "server", @"D:\state.db", ["http://localhost:5080"],
                    "Information", true, "operator"));
            }

            /// <summary>
            /// Возвращает одну известную папку.
            /// </summary>
            public Task<IReadOnlyList<ServerFolderDto>> GetFoldersAsync(CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<ServerFolderDto>>([new("folder", @"D:\data", 3, 2)]);

            /// <summary>
            /// Имитирует две страницы или отменяемое ожидание.
            /// </summary>
            public Task<DeletionPageDto> GetDeletionsAsync(string folderId, long after, CancellationToken cancellationToken)
            {
                Cursors.Add(after);
                if (Pending is not null)
                {
                    return Pending.Task.WaitAsync(cancellationToken);
                }

                return Task.FromResult(new DeletionPageDto([new(after + 1, $"file-{after}.txt", "server",
                    DateTimeOffset.UtcNow, 1_234_567, DateTimeOffset.UtcNow, true)], after == 0 ? 1 : null));
            }

            /// <summary>
            /// Отмечает освобождение подключения.
            /// </summary>
            public void Dispose() => Disposed = true;
        }
    }
}
