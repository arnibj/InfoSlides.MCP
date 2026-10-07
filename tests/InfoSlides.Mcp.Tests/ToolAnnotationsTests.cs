// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Reflection;
using InfoSlides.Mcp.Tools;
using ModelContextProtocol.Server;
using Xunit;

namespace InfoSlides.Mcp.Tests;

public sealed class ToolAnnotationsTests
{
    /// <summary>Every tool class in the CLI assembly, found by attribute so a new class cannot be missed.</summary>
    private static readonly Type[] ToolClasses = typeof(TenantTools).Assembly.GetTypes()
        .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToArray();

    private sealed record ToolMeta(
        string ToolName,
        string MethodName,
        string ClassName,
        string? Title,
        bool? ReadOnly,
        bool? Destructive,
        bool? OpenWorld,
        bool? Idempotent);

    private static List<ToolMeta> GetAllTools()
    {
        var tools = new List<ToolMeta>();
        foreach (var cls in ToolClasses)
        {
            var methods = cls.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                var cad = method.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(McpServerToolAttribute));
                if (cad == null)
                {
                    continue;
                }

                string? name = null;
                string? title = null;
                bool? readOnly = null;
                bool? destructive = null;
                bool? openWorld = null;
                bool? idempotent = null;

                foreach (var namedArg in cad.NamedArguments)
                {
                    switch (namedArg.MemberName)
                    {
                        case "Name":
                            name = namedArg.TypedValue.Value as string;
                            break;
                        case "Title":
                            title = namedArg.TypedValue.Value as string;
                            break;
                        case "ReadOnly":
                            readOnly = namedArg.TypedValue.Value as bool?;
                            break;
                        case "Destructive":
                            destructive = namedArg.TypedValue.Value as bool?;
                            break;
                        case "OpenWorld":
                            openWorld = namedArg.TypedValue.Value as bool?;
                            break;
                        case "Idempotent":
                            idempotent = namedArg.TypedValue.Value as bool?;
                            break;
                    }
                }

                tools.Add(new ToolMeta(
                    name ?? method.Name,
                    method.Name,
                    cls.Name,
                    title,
                    readOnly,
                    destructive,
                    openWorld,
                    idempotent));
            }
        }

        return tools;
    }

    [Fact]
    public void AllTools_HaveExplicitAnnotations()
    {
        var tools = GetAllTools();
        Assert.NotEmpty(tools);

        var missing = new List<string>();
        foreach (var t in tools)
        {
            if (string.IsNullOrWhiteSpace(t.Title))
            {
                missing.Add($"{t.ClassName}.{t.MethodName} ({t.ToolName}): missing Title");
            }
            if (t.ReadOnly == null)
            {
                missing.Add($"{t.ClassName}.{t.MethodName} ({t.ToolName}): missing ReadOnly");
            }
            if (t.Destructive == null)
            {
                missing.Add($"{t.ClassName}.{t.MethodName} ({t.ToolName}): missing Destructive");
            }
            if (t.OpenWorld == null)
            {
                missing.Add($"{t.ClassName}.{t.MethodName} ({t.ToolName}): missing OpenWorld");
            }
            if (t.Idempotent == null)
            {
                missing.Add($"{t.ClassName}.{t.MethodName} ({t.ToolName}): missing Idempotent");
            }
        }

        Assert.True(missing.Count == 0, "Tools missing explicit annotations:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void ToolNames_AreUniqueAndAtMost64Characters()
    {
        var tools = GetAllTools();
        Assert.NotEmpty(tools);
        Assert.All(tools, t => Assert.True(t.ToolName.Length <= 64, $"{t.ToolName} is longer than 64 characters"));
        Assert.Equal(tools.Count, tools.Select(t => t.ToolName).Distinct().Count());
    }

    [Fact]
    public void ReadOnlyTools_AreIdempotent()
    {
        foreach (var t in GetAllTools().Where(t => t.ReadOnly == true))
        {
            Assert.True(t.Idempotent, $"{t.ToolName} is ReadOnly but not Idempotent");
        }
    }

    [Fact]
    public void ReadOnlyTools_AreNeverDestructive()
    {
        var tools = GetAllTools();
        foreach (var t in tools.Where(t => t.ReadOnly == true))
        {
            Assert.False(t.Destructive, $"{t.ToolName} is marked ReadOnly=true but also Destructive=true");
        }
    }

    [Fact]
    public void OpenWorldTools_MatchExpectedSet()
    {
        var expectedOpenWorld = new HashSet<string>
        {
            "show_media_on_device",
            "add_media_slide",
            "add_designed_slide",
            "create_source",
            "update_source_settings",
            "make_ai_slide"
        };

        var tools = GetAllTools();
        var actualOpenWorld = tools.Where(t => t.OpenWorld == true).Select(t => t.ToolName).ToHashSet();

        Assert.Equal(expectedOpenWorld, actualOpenWorld);
    }

    [Fact]
    public void DestructiveTools_MatchExpectedSet()
    {
        var expectedDestructive = new HashSet<string>
        {
            "revoke_api_key",
            "delete_schedule_entry",
            "end_takeover",
            "assign_schedule",
            "create_takeover",
            "play_slideshow_find_device",
            "set_slide_conditions",
            "delete_slideshow",
            "replace_slideshow_file",
            "delete_slide",
            "revoke_team_invitation",
            "remove_team_member",
            "delete_source",
            "undo_change"
        };

        var tools = GetAllTools();
        var actualDestructive = tools.Where(t => t.Destructive == true).Select(t => t.ToolName).ToHashSet();

        Assert.Equal(expectedDestructive, actualDestructive);
    }
}