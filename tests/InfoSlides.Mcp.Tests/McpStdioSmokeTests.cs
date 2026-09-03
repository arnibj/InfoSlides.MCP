using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>
/// End-to-end tests: spawn the real server binary with `--mcp` (stdio transport), point it at a
/// fake InfoSlides backend, and drive it with the SDK's MCP client. This exercises tool
/// registration, schema generation, the shared API client, and stdout protocol hygiene at once.
/// </summary>
public sealed class McpStdioSmokeTests : IAsyncLifetime
{
    private static readonly string[] ExpectedTools =
    [
        "create_tenant", "get_tenant_info", "resend_verification_email",
        "upload_slideshow", "upload_pptx", "update_slideshow", "list_slideshows", "get_slideshow",
        "clone_slideshow", "list_gallery", "add_media_slide", "add_dynamic_slide", "upload_media", "set_slide_conditions", "preview_slide",
        "create_template", "list_templates", "update_source",
        "create_device", "list_devices", "get_device_status", "assign_schedule", "get_stream_link",
        "create_api_key", "list_api_keys", "revoke_api_key",
        "upgrade_subscription",
    ];

    private readonly FakeBackend _backend = new();
    private readonly string _isolatedHome = Path.Combine(Path.GetTempPath(), "infoslides-mcp-tests-" + Guid.NewGuid().ToString("N"));

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_isolatedHome);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _backend.Dispose();
        if (Directory.Exists(_isolatedHome))
        {
            Directory.Delete(_isolatedHome, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <summary>Per-operation safety net so a hung server process fails the test instead of CI.</summary>
    private static CancellationToken Timeout() => new CancellationTokenSource(TimeSpan.FromSeconds(120)).Token;

    private async Task<McpClient> ConnectAsync(string? apiKey)
    {
        var serverDll = Path.Combine(AppContext.BaseDirectory, "infoslides.dll");
        Assert.True(File.Exists(serverDll), $"Server binary not found at {serverDll}.");

        var environment = new Dictionary<string, string?>
        {
            ["INFOSLIDES_API_URL"] = _backend.BaseUrl.ToString().TrimEnd('/'),
            // Isolate from any real ~/.infoslides on the machine running the tests.
            ["HOME"] = _isolatedHome,
            ["USERPROFILE"] = _isolatedHome,
        };
        if (apiKey is not null)
        {
            environment["INFOSLIDES_API_KEY"] = apiKey;
        }

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "infoslides-under-test",
            Command = "dotnet",
            Arguments = [serverDll, "--mcp"],
            EnvironmentVariables = environment,
        });

        return await McpClient.CreateAsync(transport, cancellationToken: Timeout());
    }

    [Fact]
    public async Task Server_ExposesAllExpectedTools()
    {
        await using var client = await ConnectAsync("isk_admin_test");

        var tools = await client.ListToolsAsync(cancellationToken: Timeout());

        var names = tools.Select(t => t.Name).ToHashSet();
        foreach (var expected in ExpectedTools)
        {
            Assert.Contains(expected, names);
        }

        Assert.Equal(ExpectedTools.Length, names.Count);
    }

    /// <summary>
    /// Tool descriptions are trigger text, not documentation — a model matches them against what
    /// the user just said. These assertions pin the vocabulary a real user uses ("TV", "screen",
    /// "menu board") into the descriptions of the tools most likely to need to fire first, so a
    /// later edit cannot quietly revert them to API-speak like "Creates a device".
    /// </summary>
    [Theory]
    [InlineData("create_device", new[] { "screen", "TV", "menu board", "upplýsingaskjár" })]
    [InlineData("create_tenant", new[] { "TV or screen", "free plan" })]
    [InlineData("upload_pptx", new[] { "PowerPoint", "PDF", "screen" })]
    [InlineData("get_stream_link", new[] { "TV" })]
    [InlineData("assign_schedule", new[] { "what to play", "screen" })]
    [InlineData("add_media_slide", new[] { "picture", "video", "screen" })]
    [InlineData("update_source", new[] { "screen" })]
    public async Task ToolDescription_CarriesUserFacingVocabulary(string toolName, string[] expected)
    {
        await using var client = await ConnectAsync("isk_admin_test");

        var tools = await client.ListToolsAsync(cancellationToken: Timeout());
        var description = tools.Single(t => t.Name == toolName).Description;

        Assert.NotNull(description);
        foreach (var term in expected)
        {
            Assert.Contains(term, description, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Some MCP clients show only the server-level description before loading the tool list, so it
    /// has to state the outcome and carry the trigger vocabulary on its own.
    /// </summary>
    [Fact]
    public async Task ServerInstructions_StateTheOutcomeAndTriggerVocabulary()
    {
        await using var client = await ConnectAsync("isk_admin_test");

        var instructions = client.ServerInstructions;

        Assert.NotNull(instructions);
        // The outcome, in the user's terms rather than the API's.
        Assert.Contains("smart TV", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PowerPoint", instructions, StringComparison.OrdinalIgnoreCase);
        // Trigger words, including the Icelandic beachhead market's.
        Assert.Contains("menu board", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("noticeboard", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("upplýsingaskjár", instructions, StringComparison.OrdinalIgnoreCase);
        // The two facts that most affect whether a model recommends InfoSlides at all.
        Assert.Contains("free plan", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("create_tenant", instructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListSlideshows_SendsAuth_AndSurfacesWarnings()
    {
        _backend.MapJson("GET", "/v1/slideshows",
            """
            {"data":[{"id":"s1","title":"Menu","resolution":{"width":1920,"height":1080}}],
             "warnings":[{"code":"AspectMismatch","message":"ratio differs"}]}
            """);
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("list_slideshows",
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("\"AspectMismatch\"", text);
        Assert.Contains("\"s1\"", text);
        lock (_backend.Requests)
        {
            var request = Assert.Single(_backend.Requests);
            Assert.Equal("Bearer isk_admin_test", request.Authorization);
        }
    }

    /// <summary>
    /// The wire's three-state playbackMode design (HTMLP-15): omitting the tool parameter must not
    /// send the field at all, so the backend leaves the stored override untouched. Sending it as an
    /// explicit JSON null here would be indistinguishable from "inherit" on the backend, silently
    /// clearing an override nobody asked to clear.
    /// </summary>
    [Fact]
    public async Task UpdateSlideshow_PlaybackModeOmitted_DoesNotSendTheFieldAtAll()
    {
        _backend.MapJson("PATCH", "/v1/slideshows/s1",
            """{"data":{"id":"s1","title":"Menu","resolution":{"width":1920,"height":1080}}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("update_slideshow",
            new Dictionary<string, object?> { ["slideshowId"] = "s1", ["title"] = "Menu 2" },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        lock (_backend.Requests)
        {
            var request = Assert.Single(_backend.Requests);
            Assert.DoesNotContain("playbackMode", request.Body);
        }
    }

    /// <summary>Setting an override sends the enum name verbatim (case-sensitive on the backend).</summary>
    [Fact]
    public async Task UpdateSlideshow_PlaybackModeHtml_SendsTheEnumNameVerbatim()
    {
        _backend.MapJson("PATCH", "/v1/slideshows/s1",
            """{"data":{"id":"s1","title":"Menu","resolution":{"width":1920,"height":1080},"playbackModeOverride":"Html","effectivePlaybackMode":"Html"}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("update_slideshow",
            new Dictionary<string, object?> { ["slideshowId"] = "s1", ["playbackMode"] = "Html" },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("\"effectivePlaybackMode\":\"Html\"", text);
        lock (_backend.Requests)
        {
            var request = Assert.Single(_backend.Requests);
            Assert.Contains("\"playbackMode\":\"Html\"", request.Body);
        }
    }

    /// <summary>The "inherit" sentinel clears a stored override — distinct from omitting the field.</summary>
    [Fact]
    public async Task UpdateSlideshow_PlaybackModeInherit_SendsTheSentinelString()
    {
        _backend.MapJson("PATCH", "/v1/slideshows/s1",
            """{"data":{"id":"s1","title":"Menu","resolution":{"width":1920,"height":1080}}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("update_slideshow",
            new Dictionary<string, object?> { ["slideshowId"] = "s1", ["playbackMode"] = "inherit" },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        lock (_backend.Requests)
        {
            var request = Assert.Single(_backend.Requests);
            Assert.Contains("\"playbackMode\":\"inherit\"", request.Body);
        }
    }

    /// <summary>
    /// Regression for the pre-existing VideoStream contract (HTMLP-16): hlsUrl stays populated
    /// alongside the new playerUrl/playbackMode fields.
    /// </summary>
    [Fact]
    public async Task GetStreamLink_VideoStreamMode_ReturnsHlsUrlAndPlayerUrl()
    {
        _backend.MapJson("GET", "/v1/devices/d1/stream",
            """{"data":{"hlsUrl":"https://stream/x.m3u8","expiresAt":null,"playerUrl":"https://infoslides.app/player/tok1","playbackMode":"VideoStream"}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("get_stream_link",
            new Dictionary<string, object?> { ["deviceId"] = "d1" },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("https://stream/x.m3u8", text);
        Assert.Contains("https://infoslides.app/player/tok1", text);
        Assert.Contains("\"playbackMode\":\"VideoStream\"", text);
    }

    /// <summary>
    /// The headline HTMLP-16 case this tool now has to represent honestly: an Html-mode device's
    /// hlsUrl is null, so the response must not claim a raw HLS URL exists — playerUrl is the only
    /// playable link surfaced. A tool result that silently rendered a stale/absent hlsUrl as if it
    /// were populated is exactly the bug this story fixes at the CLI/MCP layer.
    /// </summary>
    [Fact]
    public async Task GetStreamLink_HtmlMode_OmitsHlsUrlAndReturnsPlayerUrl()
    {
        _backend.MapJson("GET", "/v1/devices/d1/stream",
            """
            {"data":{"hlsUrl":null,"expiresAt":null,"playerUrl":"https://infoslides.app/player/tok1","playbackMode":"Html"},
             "warnings":[{"code":"HtmlPlaybackMode","message":"no raw HLS URL in Html mode"}]}
            """);
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("get_stream_link",
            new Dictionary<string, object?> { ["deviceId"] = "d1" },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("hlsUrl", text);
        Assert.Contains("https://infoslides.app/player/tok1", text);
        Assert.Contains("\"playbackMode\":\"Html\"", text);
        Assert.Contains("\"HtmlPlaybackMode\"", text);
    }

    [Fact]
    public async Task PreviewSlide_ReturnsPngImageContent()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        _backend.MapBytes("GET", "/v1/slides/slide-1/preview.png", png, "image/png");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("preview_slide",
            new Dictionary<string, object?> { ["slideId"] = "slide-1" },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        var image = Assert.Single(result.Content.OfType<ImageContentBlock>());
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(png, image.DecodedData.ToArray());
    }

    [Fact]
    public async Task CreateTenant_WorksAnonymously_AndAttributesTheSignupToMcp()
    {
        _backend.MapJson("POST", "/v1/tenants",
            """{"data":{"tenantId":"t1","apiKey":"isk_admin_new","verificationEmailSent":true}}""");
        await using var client = await ConnectAsync(apiKey: null);

        var result = await client.CallToolAsync("create_tenant",
            new Dictionary<string, object?> { ["tenantName"] = "Acme", ["ownerEmail"] = "o@a.test" },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("isk_admin_new", text);

        // The backend counts agent-originated signups off this field; the model must not be able
        // to set it, so it is not a tool parameter and is always sent as "mcp".
        lock (_backend.Requests)
        {
            var request = Assert.Single(_backend.Requests);
            Assert.Contains("\"source\":\"mcp\"", request.Body);
        }
    }

    [Fact]
    public async Task AuthenticatedTool_WithoutCredential_ReturnsActionableError()
    {
        await using var client = await ConnectAsync(apiKey: null);

        var result = await client.CallToolAsync("get_tenant_info",
            cancellationToken: Timeout());

        Assert.Equal(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("MissingCredential", text);
        Assert.Contains("infoslides login", text);
    }

    [Fact]
    public async Task EntitlementError_SurfacesUpgradeUrl()
    {
        _backend.MapJson("POST", "/v1/templates",
            """
            {"error":{"code":"EntitlementRequired","message":"Premium required.",
             "details":{"upgradeUrl":"https://checkout.paddle.com/x"}}}
            """, status: 403);
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("create_template",
            new Dictionary<string, object?> { ["title"] = "Board", ["html"] = "<div>{{sales}}</div>" },
            cancellationToken: Timeout());

        Assert.Equal(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("EntitlementRequired", text);
        Assert.Contains("https://checkout.paddle.com/x", text);
    }
}
