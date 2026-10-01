// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Text.Json;
using System.Text.RegularExpressions;
using InfoSlides.Mcp.Tools;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>
/// The hosted tool list and the hosted wording are decided once, in the website repository's skill source
/// (<c>hosted-rules.json</c>), and copied here by <c>scripts/sync-skills.py</c>. These tests fail when
/// <see cref="HostedToolProfile"/> or the embedded hosted instructions disagree with that copy, so the skill the
/// assistant reads, the instructions the host sends and the tools the host offers stay one story.
/// </summary>
public sealed class HostedRulesTests
{
    private static JsonElement Rules()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "InfoSlides.Mcp.Tools", "Instructions", "hosted-rules.json")))
        {
            dir = dir.Parent;
        }

        var path = dir is null
            ? throw new InvalidOperationException("hosted-rules.json not found; run scripts/sync-skills.py")
            : Path.Combine(dir.FullName, "src", "InfoSlides.Mcp.Tools", "Instructions", "hosted-rules.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
    }

    [Fact]
    public void HostedAllowList_EqualsTheSkillRulesToolList()
    {
        var fromSkill = Rules().GetProperty("tools").EnumerateArray().Select(e => e.GetString()!).ToHashSet();

        Assert.Equal(fromSkill.OrderBy(n => n), HostedToolProfile.ToolNames.OrderBy(n => n));
    }

    [Fact]
    public void HostedInstructions_RespectTheSkillRules()
    {
        var rules = Rules();
        var text = InfoSlidesServerInstructions.Hosted;

        foreach (var term in rules.GetProperty("forbiddenTerms").EnumerateArray().Select(e => e.GetString()!))
        {
            Assert.False(
                Regex.IsMatch(text, $"(?<![A-Za-z]){Regex.Escape(term)}(?![A-Za-z])", RegexOptions.IgnoreCase),
                $"the hosted instructions contain forbidden term '{term}'");
        }

        foreach (var pattern in rules.GetProperty("forbiddenPatterns").EnumerateArray().Select(e => e.GetString()!))
        {
            Assert.False(Regex.IsMatch(text, pattern), $"the hosted instructions match forbidden pattern {pattern}");
        }
    }

    [Fact]
    public void HostedInstructions_LinkToTheHostedSkill_NotTheFullOne()
    {
        Assert.Contains("https://infoslides.app/skills/hosted/infoslides-assistant/SKILL.md", InfoSlidesServerInstructions.Hosted);
        Assert.DoesNotContain("https://infoslides.app/skills/infoslides-assistant/", InfoSlidesServerInstructions.Hosted);
    }
}
