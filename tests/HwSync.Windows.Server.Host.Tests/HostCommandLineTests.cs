using System.CommandLine;

namespace HwSync.Windows.Server.Host.Tests
{
    /// <summary>
    /// Проверки параметров командной строки Host.
    /// </summary>
    public class HostCommandLineTests
    {
        private static readonly string[] Expected = new[]
        {
            "Logging:LogLevel:Default=Debug",
            "Example=with spaces=and equals"
        };

        /// <summary>
        /// Проверяет передачу режима и настроек без диагностических сообщений.
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
                    Assert.That(settings, Is.EqualTo(Expected));
                });
                return Task.CompletedTask;
            });
            List<string> arguments = ["--set", "Logging:LogLevel:Default=Debug", "--set", "Example=with spaces=and equals"];
            if (consoleMode)
            {
                arguments.Add("--console");
            }

            using StringWriter output = new();
            using StringWriter error = new();
            InvocationConfiguration configuration = new()
            {
                Output = output,
                Error = error
            };

            int exitCode = await command.Parse(arguments.ToArray()).InvokeAsync(configuration);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Zero);
                Assert.That(invoked, Is.True);
                Assert.That(output.ToString(), Is.Empty);
                Assert.That(error.ToString(), Is.Empty);
            });
        }

        /// <summary>
        /// Проверяет диагностику неверных аргументов и отказ запуска сервера.
        /// </summary>
        [TestCase("--consol", "--consol")]
        [TestCase("--set", "--set")]
        [TestCase("Key=Value", "--set", "missing-equals")]
        [TestCase("Key=Value", "--set", "=missing-key")]
        [TestCase("Key=Value", "--set", "   =missing-key")]
        [TestCase("invalid", "--console=invalid")]
        public async Task Invoke_InvalidArguments_DoesNotStartHost(string expectedErrorFragment, params string[] arguments)
        {
            bool invoked = false;
            RootCommand command = HostCommandLine.CreateCommand((mode, settings, token) =>
            {
                invoked = true;
                return Task.CompletedTask;
            });

            using StringWriter output = new();
            using StringWriter error = new();
            InvocationConfiguration configuration = new()
            {
                Output = output,
                Error = error
            };

            int exitCode = await command.Parse(arguments).InvokeAsync(configuration);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Not.Zero);
                Assert.That(invoked, Is.False);
                Assert.That(error.ToString(), Does.Contain(expectedErrorFragment));
                Assert.That(output.ToString(), Does.Contain("--console").And.Contain("--set"));
            });
        }

        /// <summary>
        /// Проверяет вывод справки или версии без ошибок и запуска сервера.
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

            using StringWriter output = new();
            using StringWriter error = new();
            InvocationConfiguration configuration = new()
            {
                Output = output,
                Error = error
            };

            int exitCode = await command.Parse([argument]).InvokeAsync(configuration);

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Zero);
                Assert.That(invoked, Is.False);
                Assert.That(error.ToString(), Is.Empty);

                if (argument == "--help")
                {
                    Assert.That(output.ToString(), Does.Contain(command.Description).And.Contain("--console").And.Contain("--set"));
                }
                else
                {
                    Assert.That(output.ToString().Trim(), Does.Match(@"^\d+\.\d+\.\d+(?:[-+][^\s]+)?$"));
                }
            });
        }
    }
}
