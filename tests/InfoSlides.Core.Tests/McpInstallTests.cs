using System.Text.Json.Nodes;
using InfoSlides.Core.McpInstall;
using Xunit;

namespace InfoSlides.Core.Tests;

public sealed class McpInstallTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "infoslides-tests-" + Guid.NewGuid().ToString("N"));

    public McpInstallTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string ConfigPath => Path.Combine(_dir, "config.json");

    [Fact]
    public void ResolveLaunch_NativeBinary_UsesExecutableDirectly()
    {
        var (command, args) = McpClientConfigWriter.ResolveLaunch("/usr/local/bin/infoslides", "/opt/app");

        Assert.Equal("/usr/local/bin/infoslides", command);
        Assert.Equal(["--mcp"], args);
    }

    [Fact]
    public void ResolveLaunch_DotnetHost_IncludesDllArgument()
    {
        var (command, args) = McpClientConfigWriter.ResolveLaunch("/usr/lib/dotnet/dotnet", "/opt/app/");

        Assert.Equal("/usr/lib/dotnet/dotnet", command);
        Assert.Equal([Path.Combine("/opt/app/", "infoslides.dll"), "--mcp"], args);
    }

    [Fact]
    public void FreshInstall_CreatesConfigWithServerEntry()
    {
        var result = McpClientConfigWriter.Install(
            "claude-code", "/usr/local/bin/infoslides", ["--mcp"], "isk_admin_x", "https://api.staging.local", ConfigPath);

        Assert.Equal(ConfigPath, result.ConfigPath);
        Assert.Null(result.BackupPath);
        var root = JsonNode.Parse(File.ReadAllText(ConfigPath))!.AsObject();
        var entry = root["mcpServers"]!["infoslides"]!.AsObject();
        Assert.Equal("/usr/local/bin/infoslides", (string?)entry["command"]);
        Assert.Equal("--mcp", (string?)entry["args"]![0]);
        Assert.Equal("isk_admin_x", (string?)entry["env"]!["INFOSLIDES_API_KEY"]);
        Assert.Equal("https://api.staging.local", (string?)entry["env"]!["INFOSLIDES_API_URL"]);
    }

    [Fact]
    public void Install_MergesExistingServers_AndKeepsBackup()
    {
        File.WriteAllText(ConfigPath,
            """{"mcpServers":{"other":{"command":"other-server"}},"theme":"dark"}""");

        var result = McpClientConfigWriter.Install(
            "cursor", "/bin/infoslides", ["--mcp"], apiKey: null, apiUrl: null, ConfigPath);

        Assert.Equal(ConfigPath + ".bak", result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));
        var root = JsonNode.Parse(File.ReadAllText(ConfigPath))!.AsObject();
        Assert.Equal("other-server", (string?)root["mcpServers"]!["other"]!["command"]);
        Assert.Equal("dark", (string?)root["theme"]);
        var entry = root["mcpServers"]!["infoslides"]!.AsObject();
        Assert.Equal("/bin/infoslides", (string?)entry["command"]);
        Assert.Null(entry["env"]);
    }

    [Fact]
    public void Install_RefusesNonObjectConfig()
    {
        File.WriteAllText(ConfigPath, "[1,2,3]");

        Assert.Throws<InvalidOperationException>(() =>
            McpClientConfigWriter.Install("cursor", "/bin/infoslides", ["--mcp"], null, null, ConfigPath));
    }

    [Fact]
    public void UnknownClient_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => McpClientConfigWriter.GetDefaultConfigPath("vscode"));
    }
}
