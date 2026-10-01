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
/// Keeps the server instructions and the hosted tool profile honest: instructions only name tools that exist (and,
/// for the hosted profile, only hosted tools), the hosted text stays free of anything a directory forbids, and
/// every name in the hosted allow-list is a real tool.
/// </summary>
public sealed partial class ServerInstructionsTests
{
    /// <summary>The tool the HTTP host adds itself; it is not a CLI tool class.</summary>
    private const string HostProvidedTool = "get_user_profile";

    private static readonly string[] HostedForbiddenTerms =
    [
        "upgrade_subscription", "start_ai_studio_trial", "leave_testimonial", "create_tenant", "upgradeUrl", "trialAvailable",
        "credit card", "Capterra", "G2", "checkout", "pricing", "price", "trial", "near=", "set_screen_location",
    ];

    [GeneratedRegex("\\b[a-z]+(?:_[a-z]+)+\\b")]
    private static partial Regex ToolLikeName();

    private static Dictionary<string, MethodInfo> Tools()
    {
        var tools = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
        foreach (var type in typeof(TenantTools).Assembly.GetTypes().Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var attribute = method.GetCustomAttribute<McpServerToolAttribute>();
                if (attribute?.Name is { } name)
                {
                    tools[name] = method;
                }
            }
        }

        return tools;
    }

    [Fact]
    public void HostedText_IsShortEnoughToBeUseful()
    {
        Assert.True(InfoSlidesServerInstructions.Hosted.Length < 2000, $"{InfoSlidesServerInstructions.Hosted.Length} characters");
    }

    [Theory]
    [InlineData(ToolProfile.Full)]
    [InlineData(ToolProfile.Hosted)]
    public void Instructions_OnlyNameRealTools(ToolProfile profile)
    {
        var real = Tools().Keys.ToHashSet();
        real.Add(HostProvidedTool);

        var named = ToolLikeName().Matches(InfoSlidesServerInstructions.For(profile)).Select(m => m.Value).Distinct().ToList();

        Assert.All(named, name => Assert.True(real.Contains(name), $"'{name}' is named in the {profile} instructions but is not a tool"));
    }

    [Fact]
    public void HostedInstructions_OnlyNameHostedTools()
    {
        var named = ToolLikeName().Matches(InfoSlidesServerInstructions.Hosted).Select(m => m.Value).Distinct();

        Assert.All(named, name => Assert.True(HostedToolProfile.ToolNames.Contains(name), $"'{name}' is not in the hosted profile"));
    }

    [Fact]
    public void HostedInstructions_ContainNothingADirectoryForbids()
    {
        foreach (var term in HostedForbiddenTerms)
        {
            Assert.DoesNotContain(term, InfoSlidesServerInstructions.Hosted, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void HostedAllowList_NamesOnlyRealTools()
    {
        var real = Tools().Keys.ToHashSet();
        real.Add(HostProvidedTool);

        Assert.All(HostedToolProfile.ToolNames, name => Assert.True(real.Contains(name), $"'{name}' is in the hosted allow-list but is not a tool"));
    }

    [Fact]
    public void HostedAllowList_LeavesOutCommerceAccountsCredentialsAndLocalFiles()
    {
        string[] mustBeHidden =
        [
            "upgrade_subscription", "start_ai_studio_trial", "leave_testimonial", "create_tenant", "resend_verification_email",
            "create_api_key", "list_api_keys", "revoke_api_key", "create_source_key", "report_issue",
            "upload_media", "upload_pptx", "upload_slideshow", "replace_slideshow_file",
        ];

        Assert.Empty(HostedToolProfile.ToolNames.Intersect(mustBeHidden));
    }

    /// <summary>
    /// The review test "a destructive action without confirmation" depends on the hosting assistant asking before it
    /// deletes, which it does when the tool advertises a destructive hint. Every hosted tool that deletes, removes,
    /// revokes, ends or undoes something, plus the ones that overwrite a screen's content or a slide's rules, must
    /// carry the hint.
    /// </summary>
    [Fact]
    public void HostedDestructiveTools_AdvertiseTheDestructiveHint()
    {
        var tools = Tools();
        string[] destructivePrefixes = ["delete_", "remove_", "revoke_", "end_", "undo_", "replace_"];
        string[] alsoDestructive = ["assign_schedule", "set_slide_conditions", "create_takeover", "play_slideshow_find_device"];

        var expected = HostedToolProfile.ToolNames
            .Where(n => destructivePrefixes.Any(p => n.StartsWith(p, StringComparison.Ordinal)) || alsoDestructive.Contains(n))
            .ToList();

        Assert.Contains("delete_slideshow", expected);
        Assert.Contains("delete_slide", expected);
        Assert.All(expected, name => Assert.True(
            tools[name].GetCustomAttribute<McpServerToolAttribute>()!.Destructive,
            $"{name} must advertise a destructive hint so the assistant asks before running it"));
    }

    [Fact]
    public void HiddenParameters_AreRealParametersOfRealHostedTools()
    {
        var tools = Tools();

        foreach (var (tool, hidden) in HostedToolProfile.HiddenParameters)
        {
            Assert.Contains(tool, HostedToolProfile.ToolNames);
            var parameters = tools[tool].GetParameters().Select(p => p.Name).ToHashSet();
            Assert.All(hidden, name => Assert.True(parameters.Contains(name), $"{tool} has no parameter '{name}'"));
        }
    }
}
