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
/// APX-49: the skill's "MCP arguments are flat" table tells agents which argument names the tools take
/// where the REST body nests an object. A name that is not a real parameter is accepted by the host and
/// changes nothing, so every name in the table must exist on the tool it is listed under.
/// </summary>
public sealed class SkillArgumentNamesTests
{
    private static readonly Regex Row = new(@"^\|\s*`(?<tool>\w+)`\s*\|[^|]*\|(?<args>[^|]*)\|\s*$", RegexOptions.Multiline);

    private static string SkillText()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "skills", "infoslides-assistant", "SKILL.md")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return File.ReadAllText(Path.Combine(dir ?? throw new FileNotFoundException("skills/infoslides-assistant/SKILL.md"), "skills", "infoslides-assistant", "SKILL.md"));
    }

    private static HashSet<string> ParametersOf(string toolName)
    {
        var method = typeof(TenantTools).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .First(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name == toolName);
        return method.GetParameters().Select(p => p.Name!).ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void ArgumentTable_NamesAreRealToolParameters()
    {
        var text = SkillText();
        var section = text[text.IndexOf("## MCP arguments are flat", StringComparison.Ordinal)..];
        section = section[..section.IndexOf("\n## ", 5, StringComparison.Ordinal)];

        var rows = Row.Matches(section).ToList();
        Assert.NotEmpty(rows);
        foreach (var row in rows)
        {
            var parameters = ParametersOf(row.Groups["tool"].Value);
            foreach (Match name in Regex.Matches(row.Groups["args"].Value, "`(\\w+)`"))
            {
                Assert.True(parameters.Contains(name.Groups[1].Value),
                    $"{row.Groups["tool"].Value} has no parameter '{name.Groups[1].Value}' (skill table)");
            }
        }
    }
}
