// â”€â”€ Copyright notice â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

using System.Reflection;
using InfoSlides.Cli.Tools;
using ModelContextProtocol.Server;
using Xunit;

namespace InfoSlides.Mcp.Tests;

public sealed class ToolAnnotationsTests
{
    private static readonly Type[] ToolClasses =
    [
        typeof(ApiKeyTools),
        typeof(BillingTools),
        typeof(DeviceTools),
        typeof(MediaTools),
        typeof(ScreenTools),
        typeof(SlideshowTools),
        typeof(TemplateTools),
        typeof(TenantTools),
        typeof(WorkspaceTools)
    ];

    private sealed record ToolMeta(
        string ToolName,
        string MethodName,
        string ClassName,
        string? Title,
        bool? ReadOnly,
        bool? Destructive,
        bool? OpenWorld);

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
                    }
                }

                tools.Add(new ToolMeta(
                    name ?? method.Name,
                    method.Name,
                    cls.Name,
                    title,
                    readOnly,
                    destructive,
                    openWorld));
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
        }

        Assert.True(missing.Count == 0, "Tools missing explicit annotations:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void AllTools_TotalCountIs72()
    {
        var tools = GetAllTools();
        Assert.Equal(72, tools.Count);
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