using System.Text.Json.Nodes;
using Xunit;

namespace InfoSlides.Core.Tests;

public sealed class ClaudePluginTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.slnx").Length == 0 && dir.GetFiles("*.sln").Length == 0)
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    [Fact]
    public void ClaudePluginManifest_ExistsAndIsValid()
    {
        var root = RepoRoot();
        var manifestPath = Path.Combine(root, ".claude-plugin", "plugin.json");
        Assert.True(File.Exists(manifestPath), ".claude-plugin/plugin.json must exist at repository root.");

        var content = File.ReadAllText(manifestPath);
        var json = JsonNode.Parse(content)?.AsObject();
        Assert.NotNull(json);

        var name = (string?)json["name"];
        Assert.NotNull(name);
        Assert.Equal("infoslides", name);
        Assert.True(name.Length <= 64, "Plugin name must be 64 characters or fewer.");
        Assert.Matches("^[a-z0-9-]+$", name);

        // Verify version matches the project version
        var csprojPath = Path.Combine(root, "src", "InfoSlides.Cli", "InfoSlides.Cli.csproj");
        var csprojContent = File.ReadAllText(csprojPath);
        var expectedVersion = ExtractXmlTag(csprojContent, "Version");
        Assert.Equal(expectedVersion, (string?)json["version"]);

        Assert.NotNull((string?)json["description"]);
        Assert.NotNull((string?)json["license"]);

        // Verify hosted MCP server
        var mcpServers = json["mcpServers"]?.AsObject();
        Assert.NotNull(mcpServers);
        var infoslidesMcp = mcpServers["infoslides"]?.AsObject();
        Assert.NotNull(infoslidesMcp);
        Assert.Equal("http", (string?)infoslidesMcp["type"]);
        Assert.Equal("https://infoslides.app/mcp", (string?)infoslidesMcp["url"]);

        // Verify skills
        var skills = json["skills"]?.AsArray();
        Assert.NotNull(skills);
        Assert.Contains(skills, s => (string?)s?["name"] == "infoslides-assistant"
            && (string?)s?["path"] == "skills/infoslides-assistant/SKILL.md");
    }

    [Fact]
    public void PluginFolder_ContainsOnlySmallTextAndJsonFiles_NoBinaries()
    {
        var root = RepoRoot();
        var claudePluginDir = Path.Combine(root, ".claude-plugin");
        Assert.True(Directory.Exists(claudePluginDir));

        var files = Directory.GetFiles(claudePluginDir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            Assert.True(info.Length < 256 * 1024, $"File {file} exceeds 256 KiB limit: {info.Length} bytes.");

            var ext = info.Extension.ToLowerInvariant();
            Assert.True(ext is ".json" or ".md" or ".txt" or ".png" or ".svg" or ".jpg",
                $"Unexpected file type in .claude-plugin: {file}");
        }
    }

    [Fact]
    public void LicenseFile_ExistsAtRoot()
    {
        var root = RepoRoot();
        var licensePath = Path.Combine(root, "LICENSE");
        Assert.True(File.Exists(licensePath), "LICENSE file must exist at repository root.");

        var text = File.ReadAllText(licensePath);
        Assert.Contains("MIT License", text);
        Assert.Contains("Arni Bjorgvinsson", text);
    }

    [Fact]
    public void Readme_ContainsPrivacyPolicySectionAndLink()
    {
        var root = RepoRoot();
        var readmePath = Path.Combine(root, "README.md");
        Assert.True(File.Exists(readmePath), "README.md must exist.");

        var text = File.ReadAllText(readmePath);
        Assert.Contains("## Privacy Policy", text);
        Assert.Contains("https://infoslides.app/privacy", text);

        // Verify word count is well above 40 words
        var words = text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);
        Assert.True(words.Length >= 40, $"README.md must contain at least 40 words, found {words.Length}.");
    }

    private static string ExtractXmlTag(string xml, string tag)
    {
        var startTag = $"<{tag}>";
        var endTag = $"</{tag}>";
        var startIndex = xml.IndexOf(startTag, StringComparison.Ordinal);
        if (startIndex < 0) return string.Empty;
        startIndex += startTag.Length;
        var endIndex = xml.IndexOf(endTag, startIndex, StringComparison.Ordinal);
        if (endIndex < 0) return string.Empty;
        return xml[startIndex..endIndex].Trim();
    }
}
