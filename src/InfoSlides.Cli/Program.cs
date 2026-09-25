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

        // Response files off: options documented as "inline or @file" read the file themselves
        // (CliContext.ValueOrFile); left on, System.CommandLine splices the file's words into the
        // command line first and `--html @page.html` fails to parse.
        var parser = new ParserConfiguration { ResponseFileTokenReplacer = null };
        var exitCode = await CommandTree.Build().Parse(args, parser).InvokeAsync();

        // Give the background refresh a chance to land so the cache actually gets written for
        // short commands. Measured against the real GitHub API: a cold request (DNS + TLS +
        // response) commonly takes 600-700ms, so a shorter budget here loses the race on nearly
        // every short command and the cache never advances. Still bounded well under
        // RefreshAsync's own 5s HttpClient timeout, and never observed for faults — RefreshAsync
        // swallows its own errors by design.
        await Task.WhenAny(refresh, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);

        return exitCode;
    }

    private static string? GetOptionValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
