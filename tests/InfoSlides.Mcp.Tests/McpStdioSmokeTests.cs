using System.Text.Json;
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
        "create_template", "list_templates", "update_source", "push_data", "get_source_status", "create_source_key",
        "create_device", "list_devices", "get_device_status", "assign_schedule", "get_stream_link",
        "create_api_key", "list_api_keys", "revoke_api_key",
        "upgrade_subscription",
        "update_slide", "delete_slide", "delete_slideshow", "replace_slideshow_file", "list_sources",
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
    [InlineData("update_slideshow", new[] { "ticker", "clock", "renderStatus" })]
    [InlineData("update_slide", new[] { "hide", "Christmas", "PowerPoint" })]
    [InlineData("delete_slide", new[] { "hide", "confirm" })]
    [InlineData("delete_slideshow", new[] { "no undelete", "confirm" })]
    [InlineData("replace_slideshow_file", new[] { "PowerPoint", "same screens" })]
    [InlineData("list_sources", new[] { "ticker", "RSS" })]
    [InlineData("list_devices", new[] { "lobby", "nowPlayingSlideshowId" })]
    [InlineData("set_slide_conditions", new[] { "hide", "date", "any" })]
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

    /// <summary>APX-03: "turn off the news ticker" sends only the ticker, nothing else.</summary>
    [Fact]
    public async Task UpdateSlideshow_TickerOff_SendsOnlyTheTickerObject()
    {
        _backend.MapJson("PATCH", "/v1/slideshows/s1",
            """
            {"data":{"id":"s1","title":"Lobby","resolution":{"width":1920,"height":1080},
             "ticker":{"enabled":false},"renderStatus":"Pending",
             "screens":[{"deviceId":"d1","name":"Front lobby","playerUrl":"https://infoslides.app/player/tok1"}]}}
            """);
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("update_slideshow",
            new Dictionary<string, object?> { ["slideshowId"] = "s1", ["tickerEnabled"] = false },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("\"renderStatus\":\"Pending\"", text);
        Assert.Contains("https://infoslides.app/player/tok1", text);
        lock (_backend.Requests)
        {
            var request = Assert.Single(_backend.Requests);
            Assert.Equal("""{"ticker":{"enabled":false}}""", request.Body);
        }
    }

    [Fact]
    public async Task UpdateSlideshow_ClockDurationAndShared_AreSentAsTheirOwnFields()
    {
        _backend.MapJson("PATCH", "/v1/slideshows/s1",
            """{"data":{"id":"s1","title":"Lobby","resolution":{"width":1920,"height":1080}}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("update_slideshow",
            new Dictionary<string, object?>
            {
                ["slideshowId"] = "s1",
                ["defaultDurationSeconds"] = 15,
                ["clockEnabled"] = true,
                ["clockPosition"] = "TopRight",
                ["shared"] = true,
                ["tickerSourceIds"] = new[] { "src1" },
            },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        lock (_backend.Requests)
        {
            var body = Assert.Single(_backend.Requests).Body;
            Assert.Contains("\"defaultDurationSeconds\":15", body);
            Assert.Contains("\"clock\":{\"enabled\":true,\"position\":\"TopRight\"}", body);
            Assert.Contains("\"shared\":true", body);
            Assert.Contains("\"ticker\":{\"sourceIds\":[\"src1\"]}", body);
        }
    }

    /// <summary>APX-02/07: hide, duration and dynamic fields go to <c>PATCH /v1/slides/{id}</c>; the read-back fields survive.</summary>
    [Fact]
    public async Task UpdateSlide_HidesTheSlide_AndReturnsTypeAndHidden()
    {
        _backend.MapJson("PATCH", "/v1/slides/sl1",
            """{"data":{"id":"sl1","durationSeconds":8,"position":2,"type":"media","hidden":true,"thumbnailUrl":"https://infoslides.app/t/sl1.png"}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("update_slide",
            new Dictionary<string, object?> { ["slideId"] = "sl1", ["hidden"] = true },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("\"hidden\":true", text);
        Assert.Contains("\"type\":\"media\"", text);
        lock (_backend.Requests)
        {
            var request = Assert.Single(_backend.Requests);
            Assert.Equal("PATCH", request.Method);
            Assert.Equal("""{"hidden":true}""", request.Body);
        }
    }

    [Fact]
    public async Task UpdateSlide_DynamicFields_AreSentAsJsonNotStrings()
    {
        _backend.MapJson("PATCH", "/v1/slides/sl2",
            """{"data":{"id":"sl2","templateId":"t1","templateName":"Specials","durationSeconds":10,"position":0,"type":"dynamic"}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("update_slide",
            new Dictionary<string, object?>
            {
                ["slideId"] = "sl2",
                ["overrideData"] = JsonDocument.Parse("""{"price":"1.990 kr"}""").RootElement,
                ["sourceId"] = "src9",
            },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        lock (_backend.Requests)
        {
            var body = Assert.Single(_backend.Requests).Body;
            Assert.Contains("\"overrideData\":{\"price\":\"1.990 kr\"}", body);
            Assert.Contains("\"sourceId\":\"src9\"", body);
        }
    }

    [Fact]
    public async Task UpdateSlide_WithNothingToChange_IsRejectedWithoutCallingTheBackend()
    {
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("update_slide",
            new Dictionary<string, object?> { ["slideId"] = "sl1" }, cancellationToken: Timeout());

        Assert.True(result.IsError);
        lock (_backend.Requests)
        {
            Assert.Empty(_backend.Requests);
        }
    }

    [Fact]
    public async Task DeleteSlide_And_DeleteSlideshow_CallTheDeleteEndpoints()
    {
        _backend.MapJson("DELETE", "/v1/slides/sl1", """{"data":{}}""");
        _backend.MapJson("DELETE", "/v1/slideshows/s1", """{"data":{}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var slide = await client.CallToolAsync("delete_slide",
            new Dictionary<string, object?> { ["slideId"] = "sl1" }, cancellationToken: Timeout());
        var show = await client.CallToolAsync("delete_slideshow",
            new Dictionary<string, object?> { ["slideshowId"] = "s1" }, cancellationToken: Timeout());

        Assert.NotEqual(true, slide.IsError);
        Assert.NotEqual(true, show.IsError);
        lock (_backend.Requests)
        {
            Assert.Equal(["DELETE /v1/slides/sl1", "DELETE /v1/slideshows/s1"],
                _backend.Requests.Select(r => $"{r.Method} {r.Path}").ToArray());
        }
    }

    [Fact]
    public async Task Destructive_Tools_AreAnnotatedDestructive()
    {
        await using var client = await ConnectAsync("isk_admin_test");

        var tools = await client.ListToolsAsync(cancellationToken: Timeout());

        foreach (var name in new[] { "delete_slide", "delete_slideshow", "replace_slideshow_file" })
        {
            Assert.True(tools.Single(t => t.Name == name).ProtocolTool.Annotations?.DestructiveHint, name);
        }

        Assert.True(tools.Single(t => t.Name == "list_sources").ProtocolTool.Annotations?.ReadOnlyHint);
    }

    [Fact]
    public async Task ReplaceSlideshowFile_PutsTheFileToTheSlideshow()
    {
        _backend.MapJson("PUT", "/v1/slideshows/s1/file",
            """{"data":{"id":"s1","title":"Lobby","resolution":{"width":1920,"height":1080},"renderStatus":"Pending"}}""");
        var pptx = Path.Combine(_isolatedHome, "new-lobby.pptx");
        await File.WriteAllBytesAsync(pptx, [0x50, 0x4B, 0x03, 0x04]);
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("replace_slideshow_file",
            new Dictionary<string, object?> { ["slideshowId"] = "s1", ["filePath"] = pptx },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        lock (_backend.Requests)
        {
            var request = Assert.Single(_backend.Requests);
            Assert.Equal("PUT", request.Method);
            Assert.Contains("new-lobby.pptx", request.Body);
        }
    }

    [Fact]
    public async Task ReplaceSlideshowFile_WrongFileType_IsRejectedWithoutCallingTheBackend()
    {
        var doc = Path.Combine(_isolatedHome, "notes.docx");
        await File.WriteAllBytesAsync(doc, [1, 2, 3]);
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("replace_slideshow_file",
            new Dictionary<string, object?> { ["slideshowId"] = "s1", ["filePath"] = doc },
            cancellationToken: Timeout());

        Assert.True(result.IsError);
        lock (_backend.Requests)
        {
            Assert.Empty(_backend.Requests);
        }
    }

    [Fact]
    public async Task ListSources_ReturnsTheWorkspacesSources()
    {
        _backend.MapJson("GET", "/v1/sources",
            """{"data":[{"id":"src1","name":"BBC News","adapterType":"RssFeed","lastFetchedAt":"2026-09-25T12:00:00Z"}]}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("list_sources", cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("\"adapterType\":\"RssFeed\"", text);
    }

    /// <summary>APX-06: a date rule that hides the slide while any condition holds.</summary>
    [Fact]
    public async Task SetSlideConditions_ModeMatchAndDate_AreSentToTheBackend()
    {
        _backend.MapJson("PUT", "/v1/slides/sl1/conditions", """{"data":{}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("set_slide_conditions",
            new Dictionary<string, object?>
            {
                ["slideId"] = "sl1",
                ["conditions"] = new[] { new Dictionary<string, string> { ["type"] = "date", ["value"] = "2026-12-01..2026-12-26" } },
                ["mode"] = "hide",
                ["match"] = "any",
            },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        lock (_backend.Requests)
        {
            var body = Assert.Single(_backend.Requests).Body;
            Assert.Contains("\"type\":\"date\"", body);
            Assert.Contains("\"mode\":\"hide\"", body);
            Assert.Contains("\"match\":\"any\"", body);
        }
    }

    [Fact]
    public async Task ListDevices_ReturnsWhatEachScreenIsPlaying()
    {
        _backend.MapJson("GET", "/v1/devices",
            """{"data":[{"id":"d1","name":"Front lobby","resolution":{"width":1920,"height":1080},"nowPlayingSlideshowId":"s1","nowPlayingTitle":"Lobby"}]}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("list_devices", cancellationToken: Timeout());

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("\"nowPlayingSlideshowId\":\"s1\"", text);
        Assert.Contains("\"nowPlayingTitle\":\"Lobby\"", text);
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

    // ── LDU-05: push sources ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateTemplate_DataModePush_IsSentToTheBackend()
    {
        _backend.MapJson("POST", "/v1/templates", """{"data":{"id":"t1","title":"Queue"}}""", status: 201);
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("create_template",
            new Dictionary<string, object?> { ["title"] = "Queue", ["html"] = "<div>{{n}}</div>", ["dataMode"] = "push" },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        lock (_backend.Requests)
        {
            Assert.Contains("\"dataMode\":\"push\"", Assert.Single(_backend.Requests).Body);
        }
    }

    [Fact]
    public async Task CreateTemplate_UnknownDataMode_IsRejectedWithoutCallingTheBackend()
    {
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("create_template",
            new Dictionary<string, object?> { ["title"] = "Queue", ["html"] = "<div>{{n}}</div>", ["dataMode"] = "poll" },
            cancellationToken: Timeout());

        Assert.Equal(true, result.IsError);
        lock (_backend.Requests)
        {
            Assert.Empty(_backend.Requests);
        }
    }

    [Fact]
    public async Task AddDynamicSlide_CreatePushKey_ReturnsTheSourceAndKey()
    {
        _backend.MapJson("POST", "/v1/slideshows/s1/slides/dynamic",
            """{"data":{"id":"sl1","templateId":"t1","durationSeconds":10,"position":0,"sourceId":"src1","pushKey":"isk_dp_full"}}""",
            status: 201);
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("add_dynamic_slide",
            new Dictionary<string, object?> { ["slideshowId"] = "s1", ["templateId"] = "t1", ["createPushKey"] = true },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("src1", text);
        Assert.Contains("isk_dp_full", text);
        lock (_backend.Requests)
        {
            Assert.Contains("\"createPushKey\":true", Assert.Single(_backend.Requests).Body);
        }
    }

    [Fact]
    public async Task PushData_SendsThePayloadToTheSource()
    {
        _backend.MapJson("POST", "/v1/sources/src1/data", """{"data":{"receivedAt":"2026-09-25T12:00:00Z"}}""");
        await using var client = await ConnectAsync("isk_dp_test");

        var result = await client.CallToolAsync("push_data",
            new Dictionary<string, object?>
            {
                ["sourceId"] = "src1",
                ["data"] = JsonDocument.Parse("""{"nowServing":"A-142"}""").RootElement,
            },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        lock (_backend.Requests)
        {
            var request = Assert.Single(_backend.Requests);
            Assert.Equal("/v1/sources/src1/data", request.Path);
            Assert.Contains("A-142", request.Body);
        }
    }

    [Fact]
    public async Task GetSourceStatus_And_CreateSourceKey_CallTheSourceEndpoints()
    {
        _backend.MapJson("GET", "/v1/sources/src1",
            """{"data":{"id":"src1","name":"Queue data","lastReceivedAt":null,"hideAfterMinutes":null,"isShowingData":false,"slideIds":[]}}""");
        _backend.MapJson("POST", "/v1/sources/src1/keys",
            """{"data":{"id":"k1","name":"Push key","keyPrefix":"isk_dp_AbCd","key":"isk_dp_full"}}""", status: 201);
        await using var client = await ConnectAsync("isk_admin_test");

        var status = await client.CallToolAsync("get_source_status",
            new Dictionary<string, object?> { ["sourceId"] = "src1" }, cancellationToken: Timeout());
        var key = await client.CallToolAsync("create_source_key",
            new Dictionary<string, object?> { ["sourceId"] = "src1" }, cancellationToken: Timeout());

        Assert.NotEqual(true, status.IsError);
        Assert.Contains("isShowingData", Assert.IsType<TextContentBlock>(Assert.Single(status.Content)).Text);
        Assert.Contains("isk_dp_full", Assert.IsType<TextContentBlock>(Assert.Single(key.Content)).Text);
    }

    // ── CLI: "inline or @file" options ───────────────────────────────────────

    [Fact]
    public async Task Cli_AtFileOption_ReadsTheFile_InsteadOfExpandingItAsAResponseFile()
    {
        // System.CommandLine treats a leading @ as a response file by default and splices the
        // file's words into the command line, so `--html @page.html` failed with "Unrecognized
        // command or argument" before the command ever ran.
        _backend.MapJson("POST", "/v1/templates", """{"data":{"id":"t1","title":"Queue"}}""", status: 201);
        var html = Path.Combine(_isolatedHome, "page.html");
        await File.WriteAllTextAsync(html, "<div class=\"board\">{{nowServing}} waiting</div>");

        var psi = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "infoslides.dll"), "template", "create", "Queue", "--html", "@" + html })
        {
            psi.ArgumentList.Add(arg);
        }

        psi.Environment["INFOSLIDES_API_URL"] = _backend.BaseUrl.ToString().TrimEnd('/');
        psi.Environment["INFOSLIDES_API_KEY"] = "isk_admin_test";
        psi.Environment["HOME"] = _isolatedHome;
        psi.Environment["USERPROFILE"] = _isolatedHome;
        using var process = System.Diagnostics.Process.Start(psi)!;
        var stderr = await process.StandardError.ReadToEndAsync(Timeout());
        await process.WaitForExitAsync(Timeout());

        Assert.True(process.ExitCode == 0, stderr);
        lock (_backend.Requests)
        {
            Assert.Contains("{{nowServing}} waiting", Assert.Single(_backend.Requests).Body);
        }
    }

    /// <summary>
    /// Nullable bool options (<c>--hidden</c>, <c>--ticker</c>) must accept an explicit false as well
    /// as a bare flag, or "show the slide again" and "turn the ticker off" cannot be said.
    /// </summary>
    [Theory]
    [InlineData(new[] { "slide", "update", "sl1", "--hidden", "false" }, "PATCH", "/v1/slides/sl1", "{\"hidden\":false}")]
    [InlineData(new[] { "slide", "update", "sl1", "--hidden" }, "PATCH", "/v1/slides/sl1", "{\"hidden\":true}")]
    [InlineData(new[] { "slideshow", "update", "s1", "--ticker", "false", "--default-duration", "15" }, "PATCH", "/v1/slideshows/s1",
        "{\"defaultDurationSeconds\":15,\"ticker\":{\"enabled\":false}}")]
    [InlineData(new[] { "slide", "set-conditions", "sl1", "--condition", "date=2026-12-01..2026-12-26", "--mode", "hide", "--match", "any" },
        "PUT", "/v1/slides/sl1/conditions",
        "{\"conditions\":[{\"type\":\"date\",\"value\":\"2026-12-01..2026-12-26\"}],\"mode\":\"hide\",\"match\":\"any\"}")]
    [InlineData(new[] { "slide", "delete", "sl1" }, "DELETE", "/v1/slides/sl1", "")]
    [InlineData(new[] { "slideshow", "delete", "s1" }, "DELETE", "/v1/slideshows/s1", "")]
    [InlineData(new[] { "source", "list" }, "GET", "/v1/sources", "")]
    public async Task Cli_NewEditVerbs_SendTheExpectedRequest(string[] args, string method, string path, string body)
    {
        _backend.MapJson(method, path, """{"data":{"id":"x","title":"t","resolution":{"width":1,"height":1}}}""");
        if (method == "GET")
        {
            _backend.MapJson(method, path, """{"data":[]}""");
        }

        var psi = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "infoslides.dll"));
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        psi.Environment["INFOSLIDES_API_URL"] = _backend.BaseUrl.ToString().TrimEnd('/');
        psi.Environment["INFOSLIDES_API_KEY"] = "isk_admin_test";
        psi.Environment["HOME"] = _isolatedHome;
        psi.Environment["USERPROFILE"] = _isolatedHome;
        using var process = System.Diagnostics.Process.Start(psi)!;
        var stderr = await process.StandardError.ReadToEndAsync(Timeout());
        await process.WaitForExitAsync(Timeout());

        Assert.True(process.ExitCode == 0, stderr);
        lock (_backend.Requests)
        {
            var request = Assert.Single(_backend.Requests);
            Assert.Equal(method, request.Method);
            Assert.Equal(path, request.Path);
            Assert.Equal(body, request.Body);
        }
    }
}
