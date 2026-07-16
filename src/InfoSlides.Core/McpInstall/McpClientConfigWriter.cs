using System.Text.Json;
using System.Text.Json.Nodes;

namespace InfoSlides.Core.McpInstall;

/// <summary>
/// Writes the `infoslides --mcp` server entry into an MCP client's configuration file
/// (Claude Code, Claude Desktop, Cursor). Existing content is merged, never clobbered, and the
/// previous file is kept as a .bak backup.
/// </summary>
public static class McpClientConfigWriter
{
    public static readonly string[] SupportedClients = ["claude-code", "claude-desktop", "cursor"];

    public sealed record InstallResult(string ConfigPath, string? BackupPath);

    /// <summary>
    /// Resolves the command + args for launching this process as an MCP server. For the Native
    /// AOT binary that is the executable itself; under `dotnet infoslides.dll` the host needs the
    /// dll as its first argument.
    /// </summary>
    public static (string Command, IReadOnlyList<string> Args) ResolveLaunch(
        string? processPath, string baseDirectory)
    {
        var command = processPath ?? "infoslides";
        var fileName = Path.GetFileNameWithoutExtension(command);
        if (string.Equals(fileName, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return (command, [Path.Combine(baseDirectory, "infoslides.dll"), "--mcp"]);
        }

        return (command, ["--mcp"]);
    }

    public static InstallResult Install(
        string client,
        string command,
        IReadOnlyList<string> args,
        string? apiKey,
        string? apiUrl,
        string? overrideConfigPath = null)
    {
        var path = overrideConfigPath ?? GetDefaultConfigPath(client);

        JsonObject root;
        string? backupPath = null;
        if (File.Exists(path))
        {
            root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new InvalidOperationException(
                    $"{path} does not contain a JSON object; refusing to overwrite it.");
            backupPath = path + ".bak";
            File.Copy(path, backupPath, overwrite: true);
        }
        else
        {
            root = new JsonObject();
        }

        if (root["mcpServers"] is not JsonObject servers)
        {
            servers = new JsonObject();
            root["mcpServers"] = servers;
        }

        var argsArray = new JsonArray();
        foreach (var arg in args)
        {
            argsArray.Add((JsonNode?)JsonValue.Create(arg));
        }

        var entry = new JsonObject
        {
            ["command"] = command,
            ["args"] = argsArray,
        };
        var env = new JsonObject();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            env["INFOSLIDES_API_KEY"] = apiKey;
        }

        if (!string.IsNullOrWhiteSpace(apiUrl))
        {
            env["INFOSLIDES_API_URL"] = apiUrl;
        }

        if (env.Count > 0)
        {
            entry["env"] = env;
        }

        servers["infoslides"] = entry;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        return new InstallResult(path, backupPath);
    }

    public static string GetDefaultConfigPath(string client)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return client switch
        {
            // Project-scoped standard file, picked up automatically by Claude Code.
            "claude-code" => Path.Combine(Environment.CurrentDirectory, ".mcp.json"),
            "claude-desktop" when OperatingSystem.IsWindows() => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude", "claude_desktop_config.json"),
            "claude-desktop" when OperatingSystem.IsMacOS() => Path.Combine(
                home, "Library", "Application Support", "Claude", "claude_desktop_config.json"),
            "claude-desktop" => Path.Combine(home, ".config", "Claude", "claude_desktop_config.json"),
            "cursor" => Path.Combine(home, ".cursor", "mcp.json"),
            _ => throw new ArgumentException(
                $"Unknown MCP client '{client}'. Supported: {string.Join(", ", SupportedClients)}.", nameof(client)),
        };
    }
}
