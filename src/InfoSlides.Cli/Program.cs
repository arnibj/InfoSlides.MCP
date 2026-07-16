namespace InfoSlides.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // `--mcp` must be handled before any CLI parsing so stdio stays clean for the protocol.
        if (args.Contains("--mcp"))
        {
            return await McpBootstrap.RunAsync(
                GetOptionValue(args, "--api-key"),
                GetOptionValue(args, "--api-url"));
        }

        if (args is ["--version"] or ["-v"])
        {
            Console.WriteLine(VersionInfo.Version);
            return 0;
        }

        Console.WriteLine($"infoslides {VersionInfo.Version} — InfoSlides CLI & MCP server");
        Console.WriteLine("Run with --mcp to start the MCP server (stdio). CLI verbs arrive in the next milestone.");
        return 0;
    }

    private static string? GetOptionValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
