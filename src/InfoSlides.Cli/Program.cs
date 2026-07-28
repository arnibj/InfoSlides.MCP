using System.CommandLine;
using InfoSlides.Cli.Commands;
using InfoSlides.Core.Config;
using InfoSlides.Core.Update;

namespace InfoSlides.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // `--mcp` must be handled before any CLI parsing so stdio stays clean for the protocol.
        // The update notice is surfaced inside McpBootstrap (in the server instructions), not
        // here — stderr in MCP mode goes to the client's log file, where no user will read it.
        if (args.Contains("--mcp"))
        {
            return await McpBootstrap.RunAsync(
                GetOptionValue(args, "--api-key"),
                GetOptionValue(args, "--api-url"));
        }

        if (args is ["--version"] or ["-v"])
        {
            // Kept bare: CI and scripts compare this output exactly, so nothing else may print
            // to stdout here. The update notice would go to stderr, but suppressing it entirely
            // keeps `--version` free of side effects.
            Console.WriteLine(VersionInfo.Version);
            return 0;
        }

        var configDirectory = CredentialStore.DefaultDirectory();

        // Notice first (cache-only, no network), then kick the refresh off for next time.
        var notice = UpdateChecker.GetPendingNotice(VersionInfo.Version, configDirectory);
        if (notice is not null)
        {
            Console.Error.WriteLine(notice);
        }

        var refresh = UpdateChecker.RefreshAsync(configDirectory);

        var exitCode = await CommandTree.Build().Parse(args).InvokeAsync();

        // Give the background refresh a brief chance to land so the cache actually gets written
        // for short commands. Never allowed to delay the exit meaningfully, and never observed
        // for faults — RefreshAsync swallows its own errors by design.
        await Task.WhenAny(refresh, Task.Delay(TimeSpan.FromMilliseconds(300))).ConfigureAwait(false);

        return exitCode;
    }

    private static string? GetOptionValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
