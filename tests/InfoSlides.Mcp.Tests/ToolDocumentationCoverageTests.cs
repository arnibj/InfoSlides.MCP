// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Reflection;
using System.Text.RegularExpressions;
using InfoSlides.Mcp.Tools;
using ModelContextProtocol.Server;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>
/// APX-57: a tool that neither the skill nor <c>agents.md</c> mentions is a tool no agent learns to use. Every registered
/// tool must appear by name in the skill (<c>SKILL.md</c> and its <c>references/</c>) or in <c>agents.md</c>, unless it is
/// listed in <see cref="IntentionallyNotMentioned"/> with a reason.
/// </summary>
public sealed class ToolDocumentationCoverageTests
{
    /// <summary>Tools deliberately left out of the skill and agents.md. Empty today; add a name with a comment saying why.</summary>
    private static readonly HashSet<string> IntentionallyNotMentioned = new(StringComparer.Ordinal);

    /// <summary>Every registered tool is named in the skill or agents.md, or on the exception list.</summary>
    [Fact]
    public void EveryTool_IsMentionedInTheSkillOrAgentsMd()
    {
        var root = RepoRoot();
        var text = string.Join("\n", Directory
            .EnumerateFiles(Path.Combine(root, "skills", "infoslides-assistant"), "*.md", SearchOption.AllDirectories)
            .Select(File.ReadAllText));

        // agents.md lives in the sibling infoslides checkout (it is served from infoslides.app); counted when present.
        var agentsMd = Path.Combine(Path.GetDirectoryName(root)!, "infoslides", "src", "InfoSlides.Web", "wwwroot", "agents.md");
        if (File.Exists(agentsMd))
        {
            text += "\n" + File.ReadAllText(agentsMd);
        }

        var unmentioned = ToolNames()
            .Where(name => !IntentionallyNotMentioned.Contains(name) && !Regex.IsMatch(text, $@"\b{name}\b"))
            .Order()
            .ToList();

        Assert.True(unmentioned.Count == 0,
            "Not mentioned in the skill or agents.md (mention them, or add them to IntentionallyNotMentioned with a reason): " +
            string.Join(", ", unmentioned));
    }

    /// <summary>The exception list names only real tools, so it cannot hold on to a removed one.</summary>
    [Fact]
    public void ExceptionList_NamesOnlyRegisteredTools()
    {
        Assert.Empty(IntentionallyNotMentioned.Except(ToolNames()));
    }

    private static HashSet<string> ToolNames() => typeof(TenantTools).Assembly.GetTypes()
        .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name)
        .OfType<string>()
        .ToHashSet(StringComparer.Ordinal);

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "skills", "infoslides-assistant", "SKILL.md")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new FileNotFoundException("skills/infoslides-assistant/SKILL.md");
    }
}
