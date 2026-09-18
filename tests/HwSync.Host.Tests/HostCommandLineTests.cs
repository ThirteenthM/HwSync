using System.CommandLine;

namespace HwSync.Host.Tests
{
    /// <summary>
    /// Проверки параметров командной строки Host.
    /// </summary>
    public class HostCommandLineTests
    {
        /// <summary>
        /// Проверяет передачу режима и настроек из командной строки.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public async Task Invoke_ValidOptions_PassesModeAndSettingsToHost(bool consoleMode)
        {
            bool invoked = false;
            RootCommand command = HostCommandLine.CreateCommand((mode, settings, token) =>
            {
                invoked = true;
                Assert.Multiple(() =>
                {
                    Assert.That(mode, Is.EqualTo(consoleMode));
                    Assert.That(settings, Is.EqualTo(new[]
{
 "Logging:LogLevel:Default=Debug", "Example=with spaces=and equals"
}));
                });
                return Task.CompletedTask;
            });
            List<string> arguments = ["--set", "Logging:LogLevel:Default=Debug", "--set", "Example=with spaces=and equals"];
            if (consoleMode)
            {
                arguments.Add("--console");
            }

            int exitCode = await command.Parse(arguments.ToArray()).InvokeAsync();

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Zero);
                Assert.That(invoked, Is.True);
            });
        }

        /// <summary>
        /// Проверяет отказ запуска при неверных аргументах.
        /// </summary>
        [TestCase("--consol")]
        [TestCase("--set")]
        [TestCase("--set", "missing-equals")]
        [TestCase("--set", "=missing-key")]
        [TestCase("--set", "   =missing-key")]
        [TestCase("--console=invalid")]
        public async Task Invoke_InvalidArguments_DoesNotStartHost(params string[] arguments)
        {
            bool invoked = false;
            RootCommand command = HostCommandLine.CreateCommand((mode, settings, token) =>
            {
                invoked = true;
                return Task.CompletedTask;
            });

            int exitCode = await command.Parse(arguments).InvokeAsync();

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.Zero);
                Assert.That(invoked, Is.False);
            });
        }

        /// <summary>
        /// Проверяет вывод справки без запуска сервера.
        /// </summary>
        [TestCase("--help")]
        [TestCase("--version")]
        public async Task Invoke_InformationOption_DoesNotStartHost(string argument)
        {
            bool invoked = false;
            RootCommand command = HostCommandLine.CreateCommand((mode, settings, token) =>
            {
                invoked = true;
                return Task.CompletedTask;
            });

            int exitCode = await command.Parse([argument]).InvokeAsync();

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Zero);
                Assert.That(invoked, Is.False);
            });
        }
    }
}
