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
/// APX-70: a tool that is not in <see cref="HostedToolProfile.ToolNames"/> must have a row in
/// <c>docs/TOOL-SURFACES.md</c> with a reason, so a missing hosted tool is always a decision, never an accident.
/// </summary>
public sealed class ToolSurfaceTrackingTests
{
    private static readonly HashSet<string> Reasons =
        ["ai", "billing", "credentials", "account", "local-file", "planned", "undecided"];

    /// <summary>Every registered tool is hosted or tracked as local only.</summary>
    [Fact]
    public void EveryTool_IsHostedOrTrackedWithAReason()
    {
        var missing = RegisteredTools().Where(t => !HostedToolProfile.ToolNames.Contains(t) && !Rows().ContainsKey(t)).Order().ToList();

        Assert.True(missing.Count == 0,
            "Neither hosted nor in docs/TOOL-SURFACES.md (host it, or add a row with a reason): " + string.Join(", ", missing));
    }

    /// <summary>A row names a real tool that is not hosted, with a reason from the table, so a row dies with its gap.</summary>
    [Fact]
    public void Rows_NameOnlyRealNonHostedToolsWithKnownReasons()
    {
        var tools = RegisteredTools();
        var problems = Rows()
            .Where(r => !tools.Contains(r.Key) || HostedToolProfile.ToolNames.Contains(r.Key) || !Reasons.Contains(r.Value))
            .Select(r => $"{r.Key} ({r.Value})")
            .Order()
            .ToList();

        Assert.True(problems.Count == 0,
            "Rows that name a missing tool, a hosted tool (delete the row) or an unknown reason: " + string.Join(", ", problems));
    }

    private static Dictionary<string, string> Rows() => Regex
        .Matches(File.ReadAllText(Path.Combine(RepoRoot(), "docs", "TOOL-SURFACES.md")), @"^\| `([a-z_0-9]+)` \| `([a-z-]+)` \|", RegexOptions.Multiline)
        .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value, StringComparer.Ordinal);

    private static HashSet<string> RegisteredTools() => typeof(TenantTools).Assembly.GetTypes()
        .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name)
        .OfType<string>()
        .ToHashSet(StringComparer.Ordinal);

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "docs", "TOOL-SURFACES.md")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new FileNotFoundException("docs/TOOL-SURFACES.md");
    }
}
