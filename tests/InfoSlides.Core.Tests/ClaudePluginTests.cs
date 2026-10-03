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
        var manifestPath = Path.Combine(root, "plugins", "claude", ".claude-plugin", "plugin.json");
        Assert.True(File.Exists(manifestPath), "plugins/claude/.claude-plugin/plugin.json must exist.");

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

        // Skills are discovered from the plugin's own skills/ folder, so the manifest names none.
        Assert.Null(json["skills"]);
    }

    [Fact]
    public void ClaudePluginSkill_IsTheHostedVariant()
    {
        var skillDir = Path.Combine(RepoRoot(), "plugins", "claude", "skills", "infoslides-assistant");
        Assert.True(File.Exists(Path.Combine(skillDir, "SKILL.md")), "run scripts/sync-skills.py");

        // The plugin talks to the hosted server, so its skill must be built from the hosted rules.
        var rulesPath = Path.Combine(RepoRoot(), "src", "InfoSlides.Mcp.Tools", "Instructions", "hosted-rules.json");
        var rules = JsonNode.Parse(File.ReadAllText(rulesPath))!.AsObject();
        var tools = rules["tools"]!.AsArray().Select(t => (string)t!).ToHashSet();
        var allowed = tools.Concat(rules["allowedNonToolWords"]!.AsArray().Select(t => (string)t!)).ToHashSet();
        foreach (var file in Directory.GetFiles(skillDir, "*.md", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("surface:", text);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "`([a-z]+(?:_[a-z]+)+)`"))
            {
                Assert.True(allowed.Contains(m.Groups[1].Value), $"{Path.GetFileName(file)} names `{m.Groups[1].Value}`, which the hosted server does not offer.");
            }

            foreach (var term in rules["forbiddenTerms"]!.AsArray().Select(t => (string)t!))
            {
                Assert.DoesNotMatch($"(?i)(?<![A-Za-z]){System.Text.RegularExpressions.Regex.Escape(term)}(?![A-Za-z])", text);
            }
        }
    }

    [Fact]
    public void Marketplace_PointsAtThePluginFolder()
    {
        var market = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), ".claude-plugin", "marketplace.json")))!.AsObject();
        var plugin = market["plugins"]!.AsArray().Single()!;
        Assert.Equal("infoslides", (string?)plugin["name"]);
        Assert.True(File.Exists(Path.Combine(RepoRoot(), "plugins", "claude", ((string)plugin["source"]!)["./plugins/claude".Length..].TrimStart('/'), ".claude-plugin", "plugin.json")));
    }

    [Fact]
    public void PluginFolder_ContainsOnlySmallTextAndJsonFiles_NoBinaries()
    {
        var root = RepoRoot();
        var claudePluginDir = Path.Combine(root, "plugins", "claude");
        Assert.True(Directory.Exists(claudePluginDir));

        var files = Directory.GetFiles(claudePluginDir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            Assert.True(info.Length < 256 * 1024, $"File {file} exceeds 256 KiB limit: {info.Length} bytes.");

            var ext = info.Extension.ToLowerInvariant();
            Assert.True(ext is ".json" or ".md" or ".txt" or ".png" or ".svg" or ".jpg" || Path.GetFileName(file) == "LICENSE",
                $"Unexpected file type in the plugin folder: {file}");
        }
    }

    [Fact]
    public void LicenseFile_ExistsAtRoot()
    {
        var root = RepoRoot();
        var licensePath = Path.Combine(root, "plugins", "claude", "LICENSE");
        Assert.True(File.Exists(licensePath), "LICENSE file must exist in the plugin folder.");

        var text = File.ReadAllText(licensePath);
        Assert.Contains("MIT License", text);
        Assert.Contains("Arni Bjorgvinsson", text);
    }

    [Fact]
    public void Readme_ContainsPrivacyPolicySectionAndLink()
    {
        var root = RepoRoot();
        var readmePath = Path.Combine(root, "plugins", "claude", "README.md");
        Assert.True(File.Exists(readmePath), "README.md must exist in the plugin folder.");

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
