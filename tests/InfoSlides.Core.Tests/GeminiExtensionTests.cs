using System.Text.Json.Nodes;
using Xunit;

namespace InfoSlides.Core.Tests;

public sealed class GeminiExtensionTests
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
    public void GeminiExtensionManifest_ExistsAndIsValid()
    {
        var root = RepoRoot();
        var manifestPath = Path.Combine(root, "gemini-extension.json");
        Assert.True(File.Exists(manifestPath), "gemini-extension.json must exist at repository root.");

        var content = File.ReadAllText(manifestPath);
        var json = JsonNode.Parse(content)?.AsObject();
        Assert.NotNull(json);

        Assert.Equal("infoslides", (string?)json["name"]);
        Assert.Equal("GEMINI.md", (string?)json["contextFileName"]);

        // Verify version matches the project version
        var csprojPath = Path.Combine(root, "src", "InfoSlides.Cli", "InfoSlides.Cli.csproj");
        var csprojContent = File.ReadAllText(csprojPath);
        var expectedVersion = ExtractXmlTag(csprojContent, "Version");
        Assert.Equal(expectedVersion, (string?)json["version"]);

        // Verify MCP servers
        var mcpServers = json["mcpServers"]?.AsObject();
        Assert.NotNull(mcpServers);
        var infoslidesMcp = mcpServers["infoslides"]?.AsObject();
        Assert.NotNull(infoslidesMcp);
        Assert.Equal("infoslides", (string?)infoslidesMcp["command"]);
        var args = infoslidesMcp["args"]?.AsArray();
        Assert.NotNull(args);
        Assert.Contains("--mcp", args.Select(a => (string?)a));

        // Verify skills
        var skills = json["skills"]?.AsArray();
        Assert.NotNull(skills);
        Assert.Contains(skills, s => (string?)s?["name"] == "infoslides-assistant"
            && (string?)s?["path"] == "skills/infoslides-assistant/SKILL.md");
    }

    [Fact]
    public void PluginAndMcpConfig_ExistForAntigravityCompatibility()
    {
        var root = RepoRoot();
        var pluginJsonPath = Path.Combine(root, "plugin.json");
        var mcpConfigPath = Path.Combine(root, "mcp_config.json");

        Assert.True(File.Exists(pluginJsonPath), "plugin.json must exist for Antigravity plugin compatibility.");
        Assert.True(File.Exists(mcpConfigPath), "mcp_config.json must exist for Antigravity plugin compatibility.");

        var pluginJson = JsonNode.Parse(File.ReadAllText(pluginJsonPath))?.AsObject();
        Assert.NotNull(pluginJson);
        Assert.Equal("infoslides", (string?)pluginJson["name"]);

        var mcpConfig = JsonNode.Parse(File.ReadAllText(mcpConfigPath))?.AsObject();
        Assert.NotNull(mcpConfig);
        var mcpServers = mcpConfig["mcpServers"]?.AsObject();
        Assert.NotNull(mcpServers);
        Assert.NotNull(mcpServers["infoslides"]);
    }

    [Fact]
    public void GeminiContextFile_ExistsAndMentionsPrerequisites()
    {
        var root = RepoRoot();
        var contextPath = Path.Combine(root, "GEMINI.md");
        Assert.True(File.Exists(contextPath), "GEMINI.md context file must exist at repository root.");

        var text = File.ReadAllText(contextPath);
        Assert.Contains("infoslides --mcp", text);
        Assert.Contains("Prerequisite", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Digital Signage Assistant", text);
    }

    [Fact]
    public void BundledSkill_ExistsWithValidFrontmatterAndReferences()
    {
        var root = RepoRoot();
        var skillPath = Path.Combine(root, "skills", "infoslides-assistant", "SKILL.md");
        Assert.True(File.Exists(skillPath), "skills/infoslides-assistant/SKILL.md must exist.");

        var text = File.ReadAllText(skillPath);
        Assert.StartsWith("---", text.TrimStart());
        Assert.Contains("name: infoslides-assistant", text);

        var referencesDir = Path.Combine(root, "skills", "infoslides-assistant", "references");
        Assert.True(Directory.Exists(referencesDir), "references directory must exist in bundled skill.");
        Assert.True(Directory.GetFiles(referencesDir, "*.md").Length >= 5, "bundled skill should contain reference markdown files.");
    }

    [Fact]
    public void Readme_MentionsGeminiAndAntigravityInstall()
    {
        var root = RepoRoot();
        var readmePath = Path.Combine(root, "README.md");
        Assert.True(File.Exists(readmePath), "README.md must exist.");

        var text = File.ReadAllText(readmePath);
        Assert.Contains("gemini extensions install https://github.com/arnibj/InfoSlides.MCP", text);
        Assert.Contains("agy plugin import gemini https://github.com/arnibj/InfoSlides.MCP", text);
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
