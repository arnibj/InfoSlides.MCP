// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using InfoSlides.Mcp.Tools;
using Xunit;
using static InfoSlides.McpServer.Tests.McpRpc;

namespace InfoSlides.McpServer.Tests;

/// <summary>
/// Tool profiles over real HTTP: the hosted allow-list, hidden location parameters, scopes in tool metadata, the
/// profile tool, per-profile instructions, and that tailoring one caller's list never changes another's.
/// </summary>
public sealed class McpHostProfileTests(McpHostFactory factory) : IClassFixture<McpHostFactory>
{
    private static readonly string[] Forbidden =
    [
        "upgrade_subscription", "start_ai_studio_trial", "leave_testimonial", "create_tenant", "resend_verification_email",
        "create_api_key", "list_api_keys", "revoke_api_key", "create_source_key", "report_issue",
        "upload_media", "upload_pptx", "upload_slideshow", "replace_slideshow_file",
    ];

    private async Task<List<JsonElement>> ListToolsAsync(string bearer)
    {
        var response = await factory.CreateClient().SendAsync(Rpc(new { jsonrpc = "2.0", id = 2, method = "tools/list" }, bearer));
        var body = await ReadRpcAsync(response);
        return body.GetProperty("result").GetProperty("tools").EnumerateArray().Select(t => t.Clone()).ToList();
    }

    private async Task<JsonElement> CallAsync(string bearer, string tool, object arguments)
    {
        var response = await factory.CreateClient().SendAsync(Rpc(new { jsonrpc = "2.0", id = 3, method = "tools/call", @params = new { name = tool, arguments } }, bearer));
        return (await ReadRpcAsync(response)).GetProperty("result");
    }

    [Theory]
    [InlineData("chatgpt")]
    [InlineData("claude")]
    [InlineData("microsoft")]
    public async Task HostedCallers_SeeOnlyTheAllowListedTools(string surface)
    {
        var tools = await ListToolsAsync(factory.MintToken(surface: surface));

        var names = tools.Select(t => t.GetProperty("name").GetString()!).ToHashSet();
        Assert.Subset(HostedToolProfile.ToolNames.ToHashSet(), names);
        Assert.Empty(names.Intersect(Forbidden));
        Assert.Contains("get_user_profile", names);
        Assert.InRange(names.Count, 30, 45);
    }

    [Fact]
    public async Task ACallerWithNoSurface_IsTreatedAsHosted()
    {
        var names = (await ListToolsAsync(factory.MintToken())).Select(t => t.GetProperty("name").GetString()!).ToHashSet();

        Assert.DoesNotContain("upgrade_subscription", names);
    }

    [Fact]
    public async Task Gemini_AndApiKeys_GetTheFullSet()
    {
        var gemini = (await ListToolsAsync(factory.MintToken(surface: "gemini"))).Select(t => t.GetProperty("name").GetString()!).ToHashSet();
        var key = (await ListToolsAsync("isk_abcdefghijklmnopqrstuvwxyz012345")).Select(t => t.GetProperty("name").GetString()!).ToHashSet();

        Assert.Contains("upgrade_subscription", gemini);
        Assert.Contains("get_user_profile", gemini);
        Assert.Contains("create_api_key", key);
        Assert.DoesNotContain("get_user_profile", key);
    }

    [Fact]
    public async Task HostedSchemas_HideLocationParameters_WithoutAffectingOtherCallers()
    {
        var hostedBefore = await ListToolsAsync(factory.MintToken(surface: "chatgpt"));
        var full = await ListToolsAsync(factory.MintToken(surface: "gemini"));
        var hostedAfter = await ListToolsAsync(factory.MintToken(surface: "claude"));

        static JsonElement Props(List<JsonElement> tools, string name) =>
            tools.Single(t => t.GetProperty("name").GetString() == name).GetProperty("inputSchema").GetProperty("properties");

        Assert.False(Props(hostedBefore, "list_devices").TryGetProperty("near", out _));
        Assert.False(Props(hostedBefore, "update_device").TryGetProperty("latitude", out _));
        Assert.False(Props(hostedBefore, "add_dynamic_slide").TryGetProperty("createPushKey", out _));
        Assert.True(Props(full, "list_devices").TryGetProperty("near", out _));
        Assert.True(Props(full, "update_device").TryGetProperty("latitude", out _));
        Assert.True(Props(full, "add_dynamic_slide").TryGetProperty("createPushKey", out _));
        Assert.False(Props(hostedAfter, "list_devices").TryGetProperty("near", out _));
    }

    [Fact]
    public async Task EveryTool_AdvertisesTheScopeItNeeds_AndTheProfileToolIsMarkedForOpenAi()
    {
        var tools = await ListToolsAsync(factory.MintToken(surface: "chatgpt"));

        Assert.All(tools, t =>
        {
            var scheme = t.GetProperty("_meta").GetProperty("securitySchemes")[0];
            Assert.Equal("oauth2", scheme.GetProperty("type").GetString());
            var readOnly = t.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean();
            Assert.Equal(readOnly ? "infoslides.read" : "infoslides.write", scheme.GetProperty("scopes")[0].GetString());
        });
        var profile = tools.Single(t => t.GetProperty("name").GetString() == "get_user_profile");
        Assert.True(profile.GetProperty("_meta").GetProperty("openai/profile").GetBoolean());
    }

    [Theory]
    [InlineData("upgrade_subscription")]
    [InlineData("create_tenant")]
    [InlineData("upload_pptx")]
    public async Task HostedCallers_CannotCallToolsOutsideTheirProfile(string tool)
    {
        var result = await CallAsync(factory.MintToken(surface: "chatgpt"), tool, new { });

        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains("not available", result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task HostedCallers_CannotSendLocationParameters()
    {
        factory.ApiRequests.Clear();
        var near = await CallAsync(factory.MintToken(surface: "chatgpt"), "list_devices", new { near = "64.1,-21.9" });
        var lat = await CallAsync(factory.MintToken(surface: "chatgpt"), "update_device", new { id = "d1", latitude = 64.1, longitude = -21.9 });

        var key = await CallAsync(factory.MintToken(surface: "chatgpt"), "add_dynamic_slide", new { slideshowId = "s1", templateId = "t1", createPushKey = true });

        Assert.True(near.GetProperty("isError").GetBoolean());
        Assert.True(lat.GetProperty("isError").GetBoolean());
        Assert.True(key.GetProperty("isError").GetBoolean());
        Assert.Empty(factory.ApiRequests);
    }

    /// <summary>
    /// A <c>filePath</c> on this host would be a path on the server's own disk, never the person's, so no caller
    /// (hosted, Gemini or an API key) sees a tool that needs one or a <c>filePath</c> parameter.
    /// </summary>
    [Theory]
    [InlineData("chatgpt")]
    [InlineData("gemini")]
    [InlineData(null)]
    public async Task NoCaller_IsOfferedServerFilePaths(string? surface)
    {
        var tools = await ListToolsAsync(surface is null ? "isk_abcdefghijklmnopqrstuvwxyz012345" : factory.MintToken(surface: surface));

        var names = tools.Select(t => t.GetProperty("name").GetString()!).ToHashSet();
        Assert.DoesNotContain("upload_media", names);
        Assert.DoesNotContain("upload_pptx", names);
        Assert.DoesNotContain("replace_slideshow_file", names);
        Assert.All(tools, t => Assert.False(
            t.GetProperty("inputSchema").GetProperty("properties").TryGetProperty("filePath", out _),
            $"{t.GetProperty("name").GetString()} offers filePath"));
    }

    /// <summary>Calls that name a server file are refused before the file is touched or the API is called.</summary>
    [Theory]
    [InlineData("chatgpt", "show_media_on_device")]
    [InlineData("gemini", "upload_media")]
    [InlineData(null, "upload_pptx")]
    [InlineData(null, "show_media_on_device")]
    public async Task CallsNamingAServerFile_AreRefused(string? surface, string tool)
    {
        factory.ApiRequests.Clear();
        var bearer = surface is null ? "isk_abcdefghijklmnopqrstuvwxyz012345" : factory.MintToken(surface: surface);
        var existing = typeof(McpHostProfileTests).Assembly.Location;

        var result = await CallAsync(bearer, tool, new { deviceId = "d1", filePath = existing, title = "x" });

        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains("not available", result.GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Empty(factory.ApiRequests);
    }

    /// <summary>
    /// The hosted tool list obeys the same wording rules as the hosted skill and instructions (hosted-rules.json):
    /// no upgrades, trials or billing in tool or parameter descriptions, and no pointers to tools the hosted profile
    /// does not offer (the model would try to call them and be refused).
    /// </summary>
    [Fact]
    public async Task HostedToolDescriptions_RespectTheHostedRules()
    {
        var tools = await ListToolsAsync(factory.MintToken(surface: "chatgpt"));
        var rules = HostedRules();
        var offered = tools.Select(t => t.GetProperty("name").GetString()!).ToHashSet();
        var notOffered = (await ListToolsAsync(factory.MintToken(surface: "gemini")))
            .Select(t => t.GetProperty("name").GetString()!)
            .Concat(["upload_media", "upload_pptx", "replace_slideshow_file"])
            .Where(n => !offered.Contains(n))
            .ToHashSet();

        var problems = new List<string>();
        foreach (var tool in tools)
        {
            var name = tool.GetProperty("name").GetString()!;
            var texts = new List<(string Where, string Text)> { (name, tool.GetProperty("description").GetString() ?? string.Empty) };
            foreach (var property in tool.GetProperty("inputSchema").GetProperty("properties").EnumerateObject())
            {
                if (property.Value.TryGetProperty("description", out var description))
                {
                    texts.Add(($"{name}.{property.Name}", description.GetString() ?? string.Empty));
                }
            }

            foreach (var (where, text) in texts)
            {
                foreach (var term in rules.GetProperty("forbiddenTerms").EnumerateArray().Select(e => e.GetString()!))
                {
                    if (Regex.IsMatch(text, $"(?<![A-Za-z]){Regex.Escape(term)}(?![A-Za-z])", RegexOptions.IgnoreCase))
                    {
                        problems.Add($"{where} contains forbidden term '{term}'");
                    }
                }

                foreach (var other in notOffered.Where(n => Regex.IsMatch(text, $@"\b{n}\b")))
                {
                    problems.Add($"{where} mentions {other}, which the hosted profile does not offer");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static JsonElement HostedRules()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "InfoSlides.Mcp.Tools", "Instructions", "hosted-rules.json")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "src", "InfoSlides.Mcp.Tools", "Instructions", "hosted-rules.json"))).RootElement.Clone();
    }

    [Fact]
    public async Task ProfileTool_ReturnsAStableOpaqueIdAndTheName_AndNothingElse()
    {
        var subject = Guid.NewGuid().ToString();
        var first = await CallAsync(factory.MintToken(surface: "chatgpt", name: "Alice Example", subject: subject), "get_user_profile", new { });
        var second = await CallAsync(factory.MintToken(surface: "chatgpt", name: "Alice Example", subject: subject), "get_user_profile", new { });
        var other = await CallAsync(factory.MintToken(surface: "chatgpt", name: "Bob", subject: Guid.NewGuid().ToString()), "get_user_profile", new { });

        var a = JsonDocument.Parse(first.GetProperty("content")[0].GetProperty("text").GetString()!).RootElement;
        var b = JsonDocument.Parse(second.GetProperty("content")[0].GetProperty("text").GetString()!).RootElement;
        var c = JsonDocument.Parse(other.GetProperty("content")[0].GetProperty("text").GetString()!).RootElement;
        Assert.Equal(a.GetProperty("id").GetString(), b.GetProperty("id").GetString());
        Assert.NotEqual(a.GetProperty("id").GetString(), c.GetProperty("id").GetString());
        Assert.DoesNotContain(subject, a.GetProperty("id").GetString()!);
        Assert.Equal("Alice Example", a.GetProperty("name").GetString());
        Assert.Equal(new[] { "id", "name" }, a.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
    }

    [Theory]
    [InlineData("chatgpt", false)]
    [InlineData("gemini", true)]
    public async Task Initialize_ReturnsTheInstructionsOfTheCallersProfile(string surface, bool full)
    {
        var response = await factory.CreateClient().SendAsync(Rpc(Initialize(), factory.MintToken(surface: surface)));
        var instructions = (await ReadRpcAsync(response)).GetProperty("result").GetProperty("instructions").GetString();

        Assert.Equal(full ? InfoSlidesServerInstructions.Full : InfoSlidesServerInstructions.Hosted, instructions);
    }

    [Fact]
    public async Task ToolList_IsCachedPerCaller()
    {
        var response = await factory.CreateClient().SendAsync(Rpc(new { jsonrpc = "2.0", id = 2, method = "tools/list" }, factory.MintToken(surface: "chatgpt")));
        var result = (await ReadRpcAsync(response)).GetProperty("result");

        Assert.Equal(3600000, result.GetProperty("ttlMs").GetInt64());
        Assert.Equal("private", result.GetProperty("cacheScope").GetString());
    }
}

/// <summary>A tiny rate limit, in its own host so the other tests are unaffected.</summary>
public sealed class McpHostRateLimitTests : IClassFixture<McpHostRateLimitTests.LimitedFactory>
{
    private readonly LimitedFactory _factory;

    /// <summary>Creates the test class around a host that allows three requests a minute per caller.</summary>
    /// <param name="factory">The limited host.</param>
    public McpHostRateLimitTests(LimitedFactory factory) => _factory = factory;

    /// <summary>A host configured with three requests per minute.</summary>
    public sealed class LimitedFactory : McpHostFactory
    {
        /// <summary>Sets the limit.</summary>
        public LimitedFactory() => Settings["McpHost:RequestsPerMinute"] = "3";
    }

    [Fact]
    public async Task OneCallerIsLimited_AnotherIsNot()
    {
        var client = _factory.CreateClient();
        var busy = _factory.MintToken(surface: "chatgpt", subject: Guid.NewGuid().ToString());
        var calm = _factory.MintToken(surface: "chatgpt", subject: Guid.NewGuid().ToString());

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await client.SendAsync(Rpc(Initialize(), busy))).StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Rpc(Initialize(), calm))).StatusCode);
    }
}
