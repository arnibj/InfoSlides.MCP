using System.Text.Json;
using InfoSlides.Mcp.Tools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>Stdio smoke tests, part 4. See <see cref="McpStdioSmokeBase"/> for why they are split.</summary>
public sealed class McpStdioSmokeTests4 : McpStdioSmokeBase
{
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

    /// <summary>APX-54: ownerName reaches the wire when given and is omitted when not.</summary>
    [Fact]
    public async Task CreateTenant_OwnerName_IsSentWhenGivenAndOmittedOtherwise()
    {
        _backend.MapJson("POST", "/v1/tenants",
            """{"data":{"tenantId":"t1","apiKey":"isk_admin_new","verificationEmailSent":true}}""");
        await using var client = await ConnectAsync(apiKey: null);

        await client.CallToolAsync("create_tenant",
            new Dictionary<string, object?> { ["tenantName"] = "Acme", ["ownerEmail"] = "o@a.test", ["ownerName"] = "Anna" },
            cancellationToken: Timeout());
        await client.CallToolAsync("create_tenant",
            new Dictionary<string, object?> { ["tenantName"] = "Acme", ["ownerEmail"] = "p@a.test" },
            cancellationToken: Timeout());

        lock (_backend.Requests)
        {
            Assert.Equal(2, _backend.Requests.Count);
            Assert.Contains("\"ownerName\":\"Anna\"", _backend.Requests[0].Body);
            Assert.DoesNotContain("ownerName", _backend.Requests[1].Body);
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
            var body = Assert.Single(_backend.Requests).Body;
            Assert.Contains("\"dataMode\":\"push\"", body);
            Assert.Contains("\"css\":\"\"", body); // the API needs css in code mode; the tool sends an empty one
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
}
