using System.IO;
using HwSync.Api.Client;
using HwSync.Api.Contracts;
using HwSync.Windows.AppServices.Client;
using HwSync.Windows.AppServices.Client.ViewModels;
using HwSync.Windows.Contract.Client.Configuration;
using HwSync.Windows.Contract.Client.Services;
using HwSync.Windows.Contract.Client.ViewModels;
using HwSync.Infrastructure.FileSystem;

namespace HwSync.Client.Tests
{
    /// <summary>
    /// Проверки плана и автоматического выполнения файловых операций.
    /// </summary>
    public sealed class AutoSyncTests
    {
        /// <summary>
        /// Подтверждение выбирает сторону и переводит исключённые файлы в пропуск.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task DeletionReview_SelectsIndividualCopies(bool server)
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new());
            await model.StartCommand.ExecuteAsync(null);
            model.SetBatchDecisionCommand.Execute(new(model.Changes.Select(row => row.Path).ToArray(), FileSyncDecision.DeleteBoth));
            model.ReviewDeletions = candidates =>
            {
                Assert.That(candidates, Has.Count.EqualTo(4));
                Assert.That(candidates.Count(file => file.Path == "conflict.txt"), Is.EqualTo(2));
                return candidates.Where(file => file.Path == "conflict.txt" && file.OnServer == server).ToArray();
            };
            await model.AutoSyncCommand.ExecuteAsync(null);
            Assert.That(model.Error, Is.Empty);
            Assert.That(model.Changes.Single(row => row.Path == "conflict.txt").Action,
                Is.EqualTo(server ? FileSyncDecision.DeleteOnServer : FileSyncDecision.DeleteOnClient));
            Assert.That(model.Changes.Where(row => row.Path != "conflict.txt").All(row => row.Action == FileSyncDecision.Skip), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "conflict.txt")), Is.EqualTo(server));
            Assert.That(File.Exists(Path.Combine(root, "client.txt")), Is.True);
            Assert.That(api.Deletions, Is.EqualTo(server ? 1 : 0));
        }

        /// <summary>
        /// Отмена или пустой выбор сохраняют исходный план и файлы.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task DeletionReview_CancelKeepsPlan(bool cancel)
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new());
            await model.StartCommand.ExecuteAsync(null);
            model.SetBatchDecisionCommand.Execute(new(model.Changes.Select(row => row.Path).ToArray(), FileSyncDecision.DeleteBoth));
            ChangeRow[] original = model.Changes.ToArray();
            model.ReviewDeletions = _ => cancel ? null : [];
            await model.AutoSyncCommand.ExecuteAsync(null);
            Assert.That(model.Changes, Is.EqualTo(original));
            Assert.That(api.Deletions, Is.Zero);
            Assert.That(File.Exists(Path.Combine(root, "client.txt")), Is.True);
            Assert.That(model.CanEditPlan, Is.True);
        }

        /// <summary>
        /// Ручное удаление использует тот же выбор копий.
        /// </summary>
        [Test]
        public async Task DeletionReview_ManualDeleteUsesSelection()
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new());
            await model.StartCommand.ExecuteAsync(null);
            bool reviewed = false;
            model.ReviewDeletions = files =>
            {
                reviewed = true;
                Assert.That(files.Single().OnServer, Is.False);
                return files;
            };
            await model.DeleteClientCommand.ExecuteAsync(null);
            Assert.That(reviewed, Is.True);
            Assert.That(File.Exists(Path.Combine(root, "client.txt")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "conflict.txt")), Is.True);
        }

        /// <summary>
        /// Просмотр читает нужную сторону и отклоняет изменённый локальный файл.
        /// </summary>
        [Test]
        public async Task DeletionPreview_ReadsComparedVersions()
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new());
            await model.StartCommand.ExecuteAsync(null);
            ChangeRow row = model.Changes.Single(item => item.Path == "conflict.txt");
            DeletionCandidate local = new(row.Path, false, row.PreviousSize!.Value, row.PreviousModifiedUtc!.Value);
            DeletionCandidate remote = new(row.Path, true, row.CurrentSize!.Value, row.CurrentModifiedUtc!.Value);
            Assert.That(System.Text.Encoding.UTF8.GetString(await model.LoadDeletionPreviewAsync(local, CancellationToken.None)), Is.EqualTo("local conflict"));
            Assert.That(System.Text.Encoding.UTF8.GetString(await model.LoadDeletionPreviewAsync(remote, CancellationToken.None)), Is.EqualTo("server!!"));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await model.LoadDeletionPreviewAsync(local with
                {
                    Path = "../outside.txt"
                }, CancellationToken.None));
            File.WriteAllText(Path.Combine(root, row.Path), "changed");
            Assert.ThrowsAsync<IOException>(async () => await model.LoadDeletionPreviewAsync(local, CancellationToken.None));
        }
        /// <summary>
        /// Удаляет все имеющиеся копии только после подтверждения общего действия.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task DeleteBoth_RequiresConfirmation(bool confirmed)
        {
            string root = CreateDirectory();
            TestClient api = new()
            {
                EqualConflict = true
            };
            using MainViewModel model = CreateModel(root, api, new());
            await model.StartCommand.ExecuteAsync(null);
            model.SetBatchDecisionCommand.Execute(new(model.Changes.Select(row => row.Path).ToArray(), FileSyncDecision.DeleteBoth));
            model.ConfirmDeletion = (side, paths) =>
            {
                Assert.That(side, Does.Contain("на сервере и клиенте"));
                Assert.That(paths, Has.Count.EqualTo(3));
                return confirmed;
            };
            Assert.That(File.Exists(Path.Combine(root, "conflict.txt")), Is.True);
            await model.AutoSyncCommand.ExecuteAsync(null);
            Assert.That(api.Deletions, Is.EqualTo(confirmed ? 2 : 0));
            Assert.That(File.Exists(Path.Combine(root, "conflict.txt")), Is.EqualTo(!confirmed));
            Assert.That(File.Exists(Path.Combine(root, "client.txt")), Is.EqualTo(!confirmed));
            Assert.That(model.Error, Is.Empty);
        }

        /// <summary>
        /// Сохраняет изменённую клиентскую копию и сообщает о частичном удалении.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task DeleteBoth_ReportsIncompleteOperation(bool serverFailure)
        {
            string root = CreateDirectory();
            string path = Path.Combine(root, "conflict.txt");
            TestClient api = new()
            {
                OnComparedDelete = () =>
                {
                    if (serverFailure)
                    {
                        throw new IOException("Нет ответа");
                    }

                    File.WriteAllText(path, "changed during server operation");
                }
            };
            using MainViewModel model = CreateModel(root, api, new());
            await model.StartCommand.ExecuteAsync(null);
            foreach (ChangeRow row in model.Changes.ToArray())
            {
                model.SetFileDecisionCommand.Execute(new(row.Path,
                    row.Path == "conflict.txt" ? FileSyncDecision.DeleteBoth : FileSyncDecision.Skip, ""));
            }

            model.ConfirmDeletion = (_, _) => true;
            await model.AutoSyncCommand.ExecuteAsync(null);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(model.Error, Does.Contain(serverFailure
                ? "удаление на сервере не подтверждено" : "серверная копия удалена"));
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.False);
        }
        /// <summary>
        /// Одинаковые файлы видны, не становятся конфликтом и не требуют копирования.
        /// </summary>
        [Test]
        public async Task EqualFiles_AreVisibleAndKept()
        {
            TestClient api = new()
            {
                EqualConflict = true
            };
            using MainViewModel model = CreateModel(CreateDirectory(), api, new());
            await model.StartCommand.ExecuteAsync(null);
            ChangeRow equal = model.Changes.Single(row => row.IsUnchanged);
            Assert.That(equal.Kind, Is.EqualTo("Одинаковые"));
            Assert.That(equal.Action, Is.EqualTo(FileSyncDecision.Skip));
            Assert.That(equal.IsConflict, Is.False);
            Assert.That(model.VisibleChanges, Does.Contain(equal));
            Assert.That(equal.ActionChoices.Select(choice => choice.Action), Does.Contain(FileSyncDecision.DeleteOnClient));
            Assert.That(equal.ActionChoices.Select(choice => choice.Action), Does.Not.Contain(FileSyncDecision.KeepBoth));
            model.ShowUnchanged = false;
            Assert.That(model.VisibleChanges, Does.Not.Contain(equal));
            Assert.That(model.Changes, Does.Contain(equal));
            model.ShowUnchanged = true;
            Assert.That(model.VisibleChanges, Does.Contain(equal));
            await model.AutoSyncCommand.ExecuteAsync(null);
            Assert.That(api.Preserved, Is.Null);
            Assert.That(api.Downloads, Is.EqualTo(1));
            Assert.That(model.Error, Is.Empty);
        }
        /// <summary>
        /// Массовое копирование выбирает замену и пропускает отсутствующий источник.
        /// </summary>
        [TestCase(FileSyncDecision.CopyToClient, FileSyncDecision.ReplaceOnClient, "server.txt", "client.txt")]
        [TestCase(FileSyncDecision.CopyToServer, FileSyncDecision.ReplaceOnServer, "client.txt", "server.txt")]
        public async Task BatchDecision_UpdatesApplicableRows(FileSyncDecision direction, FileSyncDecision replacement, string copied, string skipped)
        {
            TestClient api = new();
            using MainViewModel model = CreateModel(CreateDirectory(), api, new());
            await model.StartCommand.ExecuteAsync(null);
            FileSyncDecision original = model.Changes.Single(row => row.Path == skipped).Action;
            model.SetBatchDecisionCommand.Execute(new(model.Changes.Select(row => row.Path).ToArray(), direction));
            Assert.That(model.Changes.Single(row => row.Path == copied).Action, Is.EqualTo(direction));
            Assert.That(model.Changes.Single(row => row.Path == "conflict.txt").Action, Is.EqualTo(replacement));
            Assert.That(model.Changes.Single(row => row.Path == skipped).Action, Is.EqualTo(original));
            Assert.That(model.Status, Does.Contain("Обновлено: 2. Неприменимо: 1"));
            Assert.That(api.Downloads, Is.Zero);
            Assert.That(api.Uploaded, Is.Null);
            Assert.That(api.Deletions, Is.Zero);
        }

        /// <summary>
        /// Не меняет скрытые строки даже при переданных устаревших путях выделения.
        /// </summary>
        [Test]
        public async Task BatchDecision_RejectsHiddenSelection()
        {
            using MainViewModel model = CreateModel(CreateDirectory(), new(), new());
            await model.StartCommand.ExecuteAsync(null);
            ChangeRow[] original = model.Changes.ToArray();
            model.SelectedFolderPath = "other";
            model.SetBatchDecisionCommand.Execute(new(original.Select(row => row.Path).ToArray(), FileSyncDecision.Skip));
            Assert.That(model.Changes, Is.EqualTo(original));
            Assert.That(model.Status, Does.Contain("Обновлено: 0. Неприменимо: 3"));
        }
        /// <summary>
        /// Различает соседние папки и сохраняет решения при фильтрации.
        /// </summary>
        [Test]
        public async Task Folders_FilterWithoutChangingPlan()
        {
            using MainViewModel model = CreateModel(CreateDirectory(), new(), new());
            await model.StartCommand.ExecuteAsync(null);
            ChangeRow[] rows =
            [
                new("server", "root.txt", null, 1),
                new("server", "docs/file.txt", null, 1),
                new("client", "docs/nested/file.txt", 1, null),
                new("client", "docs-other/file.txt", 1, null)
            ];
            typeof(MainViewModel).GetProperty(nameof(MainViewModel.Changes))!.SetValue(model, rows);
            Assert.That(model.Folders.Single().Children.Select(folder => folder.RelativePath), Is.EqualTo(new[] { "docs", "docs-other" }));
            model.SelectedFolderPath = "docs";
            Assert.That(model.VisibleChanges.Select(row => row.Path), Is.EqualTo(new[] { "docs/file.txt", "docs/nested/file.txt" }));
            model.IncludeSubfolders = false;
            Assert.That(model.VisibleChanges.Single().Path, Is.EqualTo("docs/file.txt"));
            model.SetFileDecisionCommand.Execute(new("docs/file.txt", FileSyncDecision.Skip, ""));
            model.SelectedFolderPath = "docs-other";
            model.SelectedFolderPath = "docs";
            Assert.That(model.VisibleChanges.Single().IsManualDecision, Is.True);
            Assert.That(model.Changes, Has.Count.EqualTo(4));
            model.SelectedFolderPath = "";
            Assert.That(model.VisibleChanges.Single().Path, Is.EqualTo("root.txt"));
            model.IncludeSubfolders = true;
            Assert.That(model.VisibleChanges, Has.Count.EqualTo(4));
        }

        /// <summary>
        /// Выполняет полный план даже при скрытых фильтром строках.
        /// </summary>
        [Test]
        public async Task Folders_HiddenRowsRemainInExecutionPlan()
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new()
            {
                ClientOnlyFiles = SyncRule.Copy,
                DifferentFiles = SyncRule.AskUser
            });
            await model.StartCommand.ExecuteAsync(null);
            model.SelectedFolderPath = "empty";
            Assert.That(model.VisibleChanges, Is.Empty);
            await model.AutoSyncCommand.ExecuteAsync(null);
            Assert.That(api.Uploaded, Is.EqualTo("client"));
            Assert.That(File.ReadAllText(Path.Combine(root, "server.txt")), Is.EqualTo("server"));
            Assert.That(model.Error, Is.Empty);
        }
        /// <summary>
        /// Ручной выбор привязан к пути и не выполняет файловые операции.
        /// </summary>
        [Test]
        public async Task RowDecision_ChangesOnlySelectedPath()
        {
            TestClient api = new();
            using MainViewModel model = CreateModel(CreateDirectory(), api, new());
            await model.StartCommand.ExecuteAsync(null);
            typeof(MainViewModel).GetProperty(nameof(MainViewModel.Changes))!
                .SetValue(model, model.Changes.Reverse().ToArray());
            model.SetFileDecisionCommand.Execute(new("server.txt", FileSyncDecision.Skip, ""));
            Assert.That(model.Changes.Single(row => row.Path == "server.txt").Action, Is.EqualTo(FileSyncDecision.Skip));
            Assert.That(model.Changes.Count(row => row.IsManualDecision), Is.EqualTo(1));
            Assert.That(api.Downloads, Is.Zero);
            Assert.That(api.Deletions, Is.Zero);
            Assert.That(model.SetFileDecisionCommand.CanExecute(new("server.txt", FileSyncDecision.CopyToServer, "")), Is.False);
            Assert.That(model.SetFileDecisionCommand.CanExecute(new("missing.txt", FileSyncDecision.Skip, "")), Is.False);
        }

        /// <summary>
        /// Удаляет выбранную версию только после подтверждения.
        /// </summary>
        [TestCase(FileSyncDecision.DeleteOnClient, false)]
        [TestCase(FileSyncDecision.DeleteOnClient, true)]
        [TestCase(FileSyncDecision.DeleteOnServer, false)]
        [TestCase(FileSyncDecision.DeleteOnServer, true)]
        public async Task RowDecision_DeletesSelectedVersion(FileSyncDecision decision, bool confirmed)
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new());
            await model.StartCommand.ExecuteAsync(null);
            foreach (ChangeRow row in model.Changes.ToArray())
            {
                model.SetFileDecisionCommand.Execute(new(row.Path,
                    row.Path == "conflict.txt" ? decision : FileSyncDecision.Skip, ""));
            }

            model.ConfirmDeletion = (_, _) => confirmed;
            await model.AutoSyncCommand.ExecuteAsync(null);
            Assert.That(model.Error, Is.Empty);
            Assert.That(api.Deletions, Is.EqualTo(confirmed && decision == FileSyncDecision.DeleteOnServer ? 1 : 0));
            Assert.That(api.Verifications, Is.EqualTo(confirmed && decision == FileSyncDecision.DeleteOnClient ? 1 : 0));
            Assert.That(File.Exists(Path.Combine(root, "conflict.txt")), Is.EqualTo(!confirmed || decision != FileSyncDecision.DeleteOnClient));
            Assert.That(File.Exists(Path.Combine(root, "client.txt")), Is.True);
            Assert.That(api.Downloads, Is.Zero);
        }
        /// <summary>
        /// Проверяет направление и безопасный пропуск при разных правилах.
        /// </summary>
        [TestCase(false, true, SyncRule.Copy, FileSyncDecision.CopyToClient)]
        [TestCase(true, false, SyncRule.Copy, FileSyncDecision.CopyToServer)]
        [TestCase(false, true, SyncRule.Delete, FileSyncDecision.DeleteOnServer)]
        [TestCase(true, false, SyncRule.Delete, FileSyncDecision.DeleteOnClient)]
        [TestCase(true, false, SyncRule.Keep, FileSyncDecision.Skip)]
        [TestCase(false, true, SyncRule.Skip, FileSyncDecision.Skip)]
        [TestCase(true, true, SyncRule.Copy, FileSyncDecision.KeepBoth)]
        [TestCase(false, false, SyncRule.Copy, FileSyncDecision.AskUser)]
        public void Strategy_ChoosesAction(bool client, bool server, SyncRule rule, FileSyncDecision expected)
        {
            ISyncDecisionService service = new SyncDecisionService();
            Assert.That(service.Decide(client, server, new()
            {
                MissingOnClient = rule,
                ClientOnlyFiles = rule
            }), Is.EqualTo(expected));
        }

        /// <summary>
        /// Отличает запрос решения от явного пропуска конфликтующих файлов.
        /// </summary>
        [TestCase(SyncRule.AskUser, FileSyncDecision.AskUser)]
        [TestCase(SyncRule.KeepBoth, FileSyncDecision.KeepBoth)]
        [TestCase(SyncRule.Skip, FileSyncDecision.Skip)]
        [TestCase(SyncRule.Keep, FileSyncDecision.Skip)]
        [TestCase(SyncRule.Copy, FileSyncDecision.AskUser)]
        public void Strategy_DifferentFilesHasExplicitMeaning(SyncRule rule, FileSyncDecision expected)
        {
            ISyncDecisionService service = new SyncDecisionService();
            Assert.That(service.Decide(true, true, new()
            {
                DifferentFiles = rule
            }), Is.EqualTo(expected));
        }

        /// <summary>
        /// Выполняет оба направления, оставляя отличающийся файл без изменений.
        /// </summary>
        [Test]
        public async Task AutoSync_CopiesBothWaysAndSkipsConflict()
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new()
            {
                ClientOnlyFiles = SyncRule.Copy,
                DifferentFiles = SyncRule.AskUser
            });
            await model.StartCommand.ExecuteAsync(null);
            Assert.That(model.Changes.Select(row => row.Action), Is.EqualTo(new[]
            {
                FileSyncDecision.CopyToClient, FileSyncDecision.CopyToServer, FileSyncDecision.AskUser
            }));
            await model.AutoSyncCommand.ExecuteAsync(null);
            Assert.That(File.ReadAllText(Path.Combine(root, "server.txt")), Is.EqualTo("server"));
            Assert.That(api.Uploaded, Is.EqualTo("client"));
            Assert.That(File.ReadAllText(Path.Combine(root, "conflict.txt")), Is.EqualTo("local conflict"));
            Assert.That(api.Downloads, Is.EqualTo(1));
            Assert.That(model.Status, Does.Contain("Выполнено: 2").And.Contain("Требуют решения: 1"));
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.False);
            Assert.That(model.CopyCommand.CanExecute(null), Is.False);
            Assert.That(model.TransferMetrics!.ConfirmedBytes, Is.EqualTo(12));
        }

        /// <summary>
        /// Сохраняет связь действия с файлом при перестановке и фильтрации строк.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task AutoSync_MatchesDecisionsByPath(bool filterServerFile)
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new()
            {
                ClientOnlyFiles = SyncRule.Copy,
                DifferentFiles = SyncRule.AskUser
            });
            await model.StartCommand.ExecuteAsync(null);
            ChangeRow[] rows = model.Changes.Reverse()
                .Where(row => !filterServerFile || row.Path != "server.txt").ToArray();
            // Имитируем замену списка будущей сортировкой или фильтром формы.
            typeof(MainViewModel).GetProperty(nameof(MainViewModel.Changes))!.SetValue(model, rows);

            await model.AutoSyncCommand.ExecuteAsync(null);

            Assert.That(api.Uploaded, Is.EqualTo("client"));
            Assert.That(api.Downloads, Is.EqualTo(filterServerFile ? 0 : 1));
            Assert.That(File.Exists(Path.Combine(root, "server.txt")), Is.EqualTo(!filterServerFile));
            Assert.That(File.ReadAllText(Path.Combine(root, "conflict.txt")), Is.EqualTo("local conflict"));
            Assert.That(model.Error, Is.Empty);
            Assert.That(model.Status, Does.Contain(filterServerFile ? "Выполнено: 1" : "Выполнено: 2"));
        }

        /// <summary>
        /// Пропускает занятый путь до или во время передачи и продолжает остальные действия.
        /// </summary>
        [TestCase(false, SyncRule.Copy)]
        [TestCase(true, SyncRule.Copy)]
        [TestCase(false, SyncRule.Delete)]
        [TestCase(true, SyncRule.Delete)]
        public async Task AutoSync_ExistingTargetDoesNotStopPlan(bool duringDownload, SyncRule nextRule)
        {
            string root = CreateDirectory();
            string target = Path.Combine(root, "server.txt");
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new()
            {
                ClientOnlyFiles = nextRule,
                DifferentFiles = SyncRule.AskUser
            });
            model.ConfirmDeletion = (_, _) => true;
            await model.StartCommand.ExecuteAsync(null);
            if (duringDownload)
            {
                api.OnDownload = () => File.WriteAllText(target, "concurrent file");
            }
            else
            {
                File.WriteAllText(target, "concurrent file");
            }

            await model.AutoSyncCommand.ExecuteAsync(null);

            Assert.That(File.ReadAllText(target), Is.EqualTo("concurrent file"));
            Assert.That(api.Uploaded, Is.EqualTo(nextRule == SyncRule.Copy ? "client" : null));
            Assert.That(File.Exists(Path.Combine(root, "client.txt")), Is.EqualTo(nextRule != SyncRule.Delete));
            Assert.That(model.Error, Is.Empty);
            Assert.That(model.Status, Does.Contain("Выполнено: 1").And.Contain("Уже существуют: 1"));
            Assert.That(model.TransferMetrics!.Files[0].ConfirmedBytes, Is.Zero);
            Assert.That(Directory.GetFiles(root, ".hwsync-*.tmp"), Is.Empty);
        }

        /// <summary>
        /// Ошибка передачи остаётся причиной остановки и не маскируется пропуском.
        /// </summary>
        [Test]
        public async Task AutoSync_DownloadErrorStopsPlan()
        {
            string root = CreateDirectory();
            TestClient api = new()
            {
                OnDownload = () => throw new IOException("Download failed")
            };
            using MainViewModel model = CreateModel(root, api, new()
            {
                ClientOnlyFiles = SyncRule.Copy,
                DifferentFiles = SyncRule.AskUser
            });
            await model.StartCommand.ExecuteAsync(null);

            await model.AutoSyncCommand.ExecuteAsync(null);

            Assert.That(api.Uploaded, Is.Null);
            Assert.That(model.Error, Does.Contain("Download failed"));
            Assert.That(File.Exists(Path.Combine(root, "server.txt")), Is.False);
            Assert.That(Directory.GetFiles(root, ".hwsync-*.tmp"), Is.Empty);
        }

        /// <summary>
        /// Отклоняет повторяющийся путь до подтверждений и файловых операций.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task AutoSync_RejectsDuplicatePaths(bool conflictingDecisions)
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new()
            {
                ClientOnlyFiles = SyncRule.Copy,
                DifferentFiles = SyncRule.AskUser
            });
            await model.StartCommand.ExecuteAsync(null);
            ChangeRow first = model.Changes.First();
            ChangeRow duplicate = first with
            {
                Action = conflictingDecisions ? FileSyncDecision.DeleteOnServer : first.Action
            };
            typeof(MainViewModel).GetProperty(nameof(MainViewModel.Changes))!
                .SetValue(model, model.Changes.Append(duplicate).ToArray());
            model.ConfirmDeletion = (_, _) => throw new AssertionException("Подтверждение не должно запрашиваться");

            await model.AutoSyncCommand.ExecuteAsync(null);

            Assert.That(api.Downloads, Is.Zero);
            Assert.That(api.Uploaded, Is.Null);
            Assert.That(api.Deletions, Is.Zero);
            Assert.That(model.Error, Does.Contain(first.Path).And.Contain("повторяется"));
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(root, "client.txt")), Is.EqualTo("client"));
        }

        /// <summary>
        /// Требует подтверждение и проверяет обе стороны перед удалением.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task AutoSync_ConfirmsDeletions(bool confirmed)
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new()
            {
                MissingOnClient = SyncRule.Delete,
                ClientOnlyFiles = SyncRule.Delete,
                DifferentFiles = SyncRule.AskUser
            });
            model.ConfirmDeletion = (_, _) => confirmed;
            await model.StartCommand.ExecuteAsync(null);
            await model.AutoSyncCommand.ExecuteAsync(null);
            Assert.That(api.Deletions, Is.EqualTo(confirmed ? 1 : 0));
            Assert.That(api.Verifications, Is.EqualTo(confirmed ? 1 : 0));
            Assert.That(File.Exists(Path.Combine(root, "client.txt")), Is.EqualTo(!confirmed));
            Assert.That(File.Exists(Path.Combine(root, "conflict.txt")), Is.True);
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.EqualTo(!confirmed));
        }

        /// <summary>
        /// Останавливает план при изменившемся после сравнения локальном файле.
        /// </summary>
        [Test]
        public async Task AutoSync_DoesNotDeleteChangedFile()
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new()
            {
                MissingOnClient = SyncRule.Skip,
                ClientOnlyFiles = SyncRule.Delete,
                DifferentFiles = SyncRule.AskUser
            });
            model.ConfirmDeletion = (_, _) => true;
            await model.StartCommand.ExecuteAsync(null);
            File.WriteAllText(Path.Combine(root, "client.txt"), "changed since comparison");
            await model.AutoSyncCommand.ExecuteAsync(null);
            Assert.That(File.ReadAllText(Path.Combine(root, "client.txt")), Is.EqualTo("changed since comparison"));
            Assert.That(model.Error, Is.Not.Empty);
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.False);
        }

        /// <summary>
        /// Передаёт отмену активной передаче и не запускает следующую операцию.
        /// </summary>
        [Test]
        public async Task AutoSync_CanCancelDownload()
        {
            string root = CreateDirectory();
            TestClient api = new()
            {
                WaitForCancellation = true
            };
            using MainViewModel model = CreateModel(root, api, new()
            {
                ClientOnlyFiles = SyncRule.Copy,
                DifferentFiles = SyncRule.AskUser
            });
            await model.StartCommand.ExecuteAsync(null);
            Task execution = model.AutoSyncCommand.ExecuteAsync(null);
            await api.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await model.CancelCommand.ExecuteAsync(null);
            await execution.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(api.Uploaded, Is.Null);
            Assert.That(File.Exists(Path.Combine(root, "server.txt")), Is.False);
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.False);
            Assert.That(model.CancelCommand.CanExecute(null), Is.False);
        }

        /// <summary>
        /// Сбрасывает план при переключении профиля даже с одинаковыми путями.
        /// </summary>
        [Test]
        public async Task ProfileChange_InvalidatesPlan()
        {
            string root = CreateDirectory();
            using MainViewModel model = CreateModel(root, new(), new());
            await model.StartCommand.ExecuteAsync(null);
            SyncProfile profile = model.SelectedProfile!;
            model.SelectedProfile = new()
            {
                ServerRootPath = profile.ServerRootPath,
                ClientRootPath = profile.ClientRootPath,
                ServerAddress = profile.ServerAddress,
                Rules = new()
                {
                    MissingOnClient = SyncRule.Delete
                }
            };
            Assert.That(model.Changes, Is.Empty);
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.False);
        }

        /// <summary>
        /// Выбор меняет только план; выполнение применяет нужную версию.
        /// </summary>
        [TestCase(FileSyncDecision.ReplaceOnClient)]
        [TestCase(FileSyncDecision.ReplaceOnServer)]
        [TestCase(FileSyncDecision.KeepBoth)]
        [TestCase(FileSyncDecision.Skip)]
        [TestCase(FileSyncDecision.AskUser)]
        public async Task Conflict_ChoiceIsDeferredUntilAutoSync(FileSyncDecision decision)
        {
            string root = CreateDirectory();
            TestClient api = new();
            using MainViewModel model = CreateModel(root, api, new()
            {
                MissingOnClient = SyncRule.Skip,
                DifferentFiles = SyncRule.AskUser
            });
            await model.StartCommand.ExecuteAsync(null);
            ChangeRow conflict = model.Changes.Single(row => row.IsConflict);
            model.ChooseConflictResolution = row =>
            {
                Assert.That(row.PreviousModifiedUtc, Is.Not.Null);
                Assert.That(row.CurrentModifiedUtc, Is.Not.Null);
                return decision;
            };
            model.ResolveConflictCommand.Execute(conflict);
            Assert.That(File.ReadAllText(Path.Combine(root, "conflict.txt")), Is.EqualTo("local conflict"));
            Assert.That(api.Replaced, Is.Null);
            Assert.That(api.Preserved, Is.Null);
            Assert.That(model.Changes.Single(row => row.IsConflict).Action, Is.EqualTo(decision));
            bool executable = decision is not (FileSyncDecision.AskUser or FileSyncDecision.Skip);
            Assert.That(model.AutoSyncCommand.CanExecute(null), Is.EqualTo(executable));
            if (!executable)
            {
                return;
            }

            await model.AutoSyncCommand.ExecuteAsync(null);

            Assert.That(model.Error, Is.Empty);
            Assert.That(File.ReadAllText(Path.Combine(root, "conflict.txt")),
                Is.EqualTo(decision == FileSyncDecision.ReplaceOnServer ? "local conflict" : "server!!"));
            Assert.That(api.Replaced, Is.EqualTo(decision == FileSyncDecision.ReplaceOnServer ? "local conflict" : null));
            Assert.That(api.Preserved, Is.EqualTo(decision == FileSyncDecision.KeepBoth ? "local conflict" : null));
            string[] backups = Directory.GetFiles(root, "*client-conflict*");
            Assert.That(backups, Has.Length.EqualTo(decision == FileSyncDecision.KeepBoth ? 1 : 0));
            if (backups.Length > 0)
            {
                Assert.That(File.ReadAllText(backups[0]), Is.EqualTo("local conflict"));
            }
        }

        /// <summary>
        /// Закрытие диалога без выбора сохраняет прежний план.
        /// </summary>
        [Test]
        public async Task Conflict_CancelKeepsPlan()
        {
            using MainViewModel model = CreateModel(CreateDirectory(), new(), new());
            await model.StartCommand.ExecuteAsync(null);
            ChangeRow conflict = model.Changes.Single(row => row.IsConflict);
            Assert.That(conflict.Action, Is.EqualTo(FileSyncDecision.KeepBoth));
            model.ChooseConflictResolution = _ => null;
            model.ResolveConflictCommand.Execute(conflict);
            Assert.That(model.Changes.Single(row => row.IsConflict), Is.EqualTo(conflict));
        }
        /// <summary>
        /// Создаёт независимую папку с локальным файлом и конфликтом.
        /// </summary>
        private static string CreateDirectory()
        {
            string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "auto-sync", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "client.txt"), "client");
            File.WriteAllText(Path.Combine(root, "conflict.txt"), "local conflict");
            return root;
        }

        /// <summary>
        /// Создаёт модель с настоящими локальными операциями и подставным сервером.
        /// </summary>
        private static MainViewModel CreateModel(string root, TestClient api, ConflictRules rules)
        {
            MainViewModel model = new(_ => api, new DirectorySnapshotProvider(), new ComparedFileOperations(),
                new SourceFileReader(), new MissingFileSynchronizer(), new SyncDecisionService(), new ConflictFileOperations(new SourceFileReader()));
            model.LoadProfiles([new()
            {
                ClientRootPath = root, ServerRootPath = Path.Combine(root, "server"), Rules = rules
            }]);
            return model;
        }

        /// <summary>
        /// Сервер сравнения и файловых операций без сетевых запросов.
        /// </summary>
        private sealed class TestClient : IHwSyncApiClient, IFileDownloadClient, IFileMutationClient, IConflictFileClient, IComparedFileMutationClient
        {
            /// <summary>
            /// Подтверждает актуальность серверной версии.
            /// </summary>
            public Task EnsureServerFileUnchangedAsync(Guid jobId, string relativePath, CancellationToken token)
            {
                Verifications++;
                return Task.CompletedTask;
            }

            /// <summary>
            /// Регистрирует удаление сравниваемой версии.
            /// </summary>
            public Task DeleteComparedServerFileAsync(Guid jobId, string relativePath, CancellationToken token)
            {
                OnComparedDelete?.Invoke();
                Deletions++;
                return Task.CompletedTask;
            }
            public Action? OnComparedDelete
            {
                get; init;
            }

            public bool EqualConflict
            {
                get; init;
            }

            public string? Replaced
            {
                get; private set;
            }

            public string? Preserved
            {
                get; private set;
            }

            public string? Uploaded
            {
                get; private set;
            }

            public Action? OnDownload
            {
                get; set;
            }

            public int Downloads
            {
                get; private set;
            }

            public int Deletions
            {
                get; private set;
            }

            public int Verifications
            {
                get; private set;
            }

            public bool WaitForCancellation
            {
                get; init;
            }

            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Возвращает готовность подставного сервера.
            /// </summary>
            public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(new HealthResponse("ok"));

            /// <summary>
            /// Возвращает два односторонних файла и один конфликт.
            /// </summary>
            public Task<ScanJobResponse> StartComparisonAsync(CompareFoldersRequest request, CancellationToken cancellationToken = default)
            {
                FileSnapshotDto local = request.ClientSnapshot.Single(file => file.RelativePath == "client.txt");
                FileSnapshotDto conflict = request.ClientSnapshot.Single(file => file.RelativePath == "conflict.txt");
                return Task.FromResult(new ScanJobResponse(Guid.NewGuid(), ScanJobState.Completed,
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                    [new(FileChangeKind.Created, null, new("server.txt", 6, DateTime.UtcNow)),
                     new(FileChangeKind.Deleted, local, null),
                     new(EqualConflict ? FileChangeKind.Unchanged : FileChangeKind.Modified, conflict, EqualConflict ? conflict : new("conflict.txt", 8, DateTime.UtcNow))], null));
            }

            /// <summary>
            /// Опрос не нужен для завершённого сравнения.
            /// </summary>
            public Task<ScanJobResponse> GetScanAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            /// <summary>
            /// Отмена сравнения не нужна для завершённого задания.
            /// </summary>
            public Task<ScanJobResponse> CancelScanAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

            /// <summary>
            /// Передаёт тестовый файл или ожидает отмены.
            /// </summary>
            public async Task DownloadFileAsync(Guid jobId, string relativePath, Stream target, CancellationToken token)
            {
                Downloads++;
                OnDownload?.Invoke();
                Started.TrySetResult();
                if (WaitForCancellation)
                {
                    await Task.Delay(Timeout.Infinite, token);
                }

                await target.WriteAsync(System.Text.Encoding.UTF8.GetBytes(relativePath == "conflict.txt" ? "server!!" : "server"), token);
            }

            /// <summary>
            /// Сохраняет переданное клиентом содержимое.
            /// </summary>
            public async Task UploadFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token)
            {
                using StreamReader reader = new(source, leaveOpen: true);
                Uploaded = await reader.ReadToEndAsync(token);
            }

            /// <summary>
            /// Запоминает версию, выбранную для замены на сервере.
            /// </summary>
            public async Task ReplaceServerFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token)
            {
                using StreamReader reader = new(source, leaveOpen: true);
                Replaced = await reader.ReadToEndAsync(token);
            }

            /// <summary>
            /// Запоминает дополнительную клиентскую копию для сохранения обеих версий.
            /// </summary>
            public async Task PreserveClientFileAsync(Guid jobId, string relativePath, Stream source, CancellationToken token)
            {
                using StreamReader reader = new(source, leaveOpen: true);
                Preserved = await reader.ReadToEndAsync(token);
            }
            /// <summary>
            /// Регистрирует удаление на сервере.
            /// </summary>
            public Task DeleteServerFileAsync(Guid jobId, string relativePath, CancellationToken token)
            {
                Deletions++;
                return Task.CompletedTask;
            }

            /// <summary>
            /// Подтверждает отсутствие клиентского файла на сервере.
            /// </summary>
            public Task EnsureServerFileMissingAsync(Guid jobId, string relativePath, CancellationToken token)
            {
                Verifications++;
                return Task.CompletedTask;
            }
        }
    }
}
