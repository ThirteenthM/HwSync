using HwSync.Api;
using System.CommandLine;
using Microsoft.Extensions.Hosting;

namespace HwSync.Host
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            RootCommand command = HostCommandLine.CreateCommand(async (consoleMode, settings, cancellationToken) =>
            {
                await using WebApplication host = HostBootstrap.CreateBuilder(consoleMode, settings).Build();
                host.MapHwSyncApi();
                await ((IHost)host).RunAsync(cancellationToken);
            });
            return await command.Parse(args).InvokeAsync();
        }
    }
}
