using System.Text.Json;
using InfoSlides.Mcp.Tools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>Stdio smoke tests, part 3. See <see cref="McpStdioSmokeBase"/> for why they are split.</summary>
public sealed class McpStdioSmokeTests3 : McpStdioSmokeBase
{
    /// <summary>APX-17: schedule windows and takeovers reach the right routes with the right bodies.</summary>
    [Fact]
    public async Task ScheduleWindowsAndTakeovers_SendTheExpectedRequests()
    {
        _backend.MapJson("GET", "/v1/devices/d1/schedule",
            """{"data":{"deviceId":"d1","timeZone":"Atlantic/Reykjavik","entries":[]}}""");
        _backend.MapJson("POST", "/v1/devices/d1/schedule/entries",
            """{"data":{"id":"e1","deviceId":"d1","slideshowId":"s1","startTime":"11:00","endTime":"14:00","priority":0,"timeZone":"Atlantic/Reykjavik"}}""", 201);
        _backend.MapJson("POST", "/v1/takeovers", """{"data":{"id":"t1","isActive":true}}""", 201);
        _backend.MapJson("GET", "/v1/takeovers", """{"data":[]}""");
        _backend.MapJson("DELETE", "/v1/takeovers/t1", "", 204);
        await using var client = await ConnectAsync("isk_admin_test");

        var schedule = await client.CallToolAsync("get_schedule",
            new Dictionary<string, object?> { ["deviceId"] = "d1" }, cancellationToken: Timeout());
        Assert.Contains("Atlantic/Reykjavik", Assert.IsType<TextContentBlock>(Assert.Single(schedule.Content)).Text);

        var added = await client.CallToolAsync("add_schedule_entry",
            new Dictionary<string, object?> { ["deviceId"] = "d1", ["slideshowId"] = "s1", ["startTime"] = "11:00", ["endTime"] = "14:00" },
            cancellationToken: Timeout());
        Assert.NotEqual(true, added.IsError);

        var taken = await client.CallToolAsync("create_takeover",
            new Dictionary<string, object?> { ["deviceIds"] = new[] { "d1" }, ["slideshowId"] = "s1", ["durationMinutes"] = 120 },
            cancellationToken: Timeout());
        Assert.NotEqual(true, taken.IsError);

        var listed = await client.CallToolAsync("list_takeovers",
            new Dictionary<string, object?> { ["activeOnly"] = true }, cancellationToken: Timeout());
        Assert.NotEqual(true, listed.IsError);

        var ended = await client.CallToolAsync("end_takeover",
            new Dictionary<string, object?> { ["takeoverId"] = "t1" }, cancellationToken: Timeout());
        Assert.NotEqual(true, ended.IsError);

        lock (_backend.Requests)
        {
            var entry = Assert.Single(_backend.Requests, r => r.Path == "/v1/devices/d1/schedule/entries");
            Assert.Contains("\"startTime\":\"11:00\"", entry.Body);
            Assert.Contains("\"endTime\":\"14:00\"", entry.Body);
            var takeover = Assert.Single(_backend.Requests, r => r.Method == "POST" && r.Path == "/v1/takeovers");
            Assert.Contains("\"deviceIds\":[\"d1\"]", takeover.Body);
            Assert.Contains("\"durationMinutes\":120", takeover.Body);
        }
    }

    /// <summary>APX-17: adapters, then create, edit and delete a pulled source.</summary>
    [Fact]
    public async Task PulledSources_ListCreateEditDelete_SendTheExpectedRequests()
    {
        _backend.MapJson("GET", "/v1/adapters",
            """{"data":[{"adapterType":"RssFeed","displayName":"RSS","description":"d","tier":1,"isGloballyAvailable":true,"configFields":"[]"}]}""");
        _backend.MapJson("POST", "/v1/sources",
            """{"data":{"id":"src1","name":"News","adapterType":"RssFeed","isEnabled":true}}""", 201);
        _backend.MapJson("PATCH", "/v1/sources/src1",
            """{"data":{"id":"src1","name":"News","adapterType":"RssFeed","isEnabled":false}}""");
        _backend.MapJson("DELETE", "/v1/sources/src1", "", 204);
        await using var client = await ConnectAsync("isk_admin_test");

        var adapters = await client.CallToolAsync("list_adapters", cancellationToken: Timeout());
        Assert.Contains("\"configFields\"", Assert.IsType<TextContentBlock>(Assert.Single(adapters.Content)).Text);

        var created = await client.CallToolAsync("create_source",
            new Dictionary<string, object?>
            {
                ["adapterType"] = "RssFeed",
                ["name"] = "News",
                ["config"] = new Dictionary<string, object?> { ["feedUrl"] = "https://example.com/rss" },
            }, cancellationToken: Timeout());
        Assert.NotEqual(true, created.IsError);

        var edited = await client.CallToolAsync("update_source_settings",
            new Dictionary<string, object?> { ["sourceId"] = "src1", ["isEnabled"] = false }, cancellationToken: Timeout());
        Assert.NotEqual(true, edited.IsError);

        var deleted = await client.CallToolAsync("delete_source",
            new Dictionary<string, object?> { ["sourceId"] = "src1" }, cancellationToken: Timeout());
        Assert.NotEqual(true, deleted.IsError);

        lock (_backend.Requests)
        {
            var create = Assert.Single(_backend.Requests, r => r.Method == "POST" && r.Path == "/v1/sources");
            Assert.Contains("\"adapterType\":\"RssFeed\"", create.Body);
            Assert.Contains("\"feedUrl\":\"https://example.com/rss\"", create.Body);
            var edit = Assert.Single(_backend.Requests, r => r.Method == "PATCH");
            Assert.Equal("{\"isEnabled\":false}", edit.Body);
        }
    }

    /// <summary>APX-59: fetch now, read the items, and test a configuration.</summary>
    [Fact]
    public async Task SourceFetchItemsAndTest_SendTheExpectedRequests()
    {
        _backend.MapJson("POST", "/v1/sources/src1/fetch", """{"data":{"queued":true,"message":"Queued"}}""");
        _backend.MapJson("GET", "/v1/sources/src1/items", """{"data":[{"title":"Headline","fetchedAt":"2026-10-16T10:00:00Z"}]}""");
        _backend.MapJson("POST", "/v1/sources/test-connection", """{"data":{"success":false,"message":"The feed address was not found (404)."}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var fetched = await client.CallToolAsync("fetch_source_now",
            new Dictionary<string, object?> { ["sourceId"] = "src1" }, cancellationToken: Timeout());
        var items = await client.CallToolAsync("get_source_items",
            new Dictionary<string, object?> { ["sourceId"] = "src1", ["limit"] = 5 }, cancellationToken: Timeout());
        var tested = await client.CallToolAsync("test_source_config",
            new Dictionary<string, object?>
            {
                ["adapterType"] = "RssFeed",
                ["config"] = new Dictionary<string, object?> { ["feedUrl"] = "https://x.test/rss" },
            }, cancellationToken: Timeout());

        Assert.NotEqual(true, fetched.IsError);
        Assert.Contains("Headline", Assert.IsType<TextContentBlock>(Assert.Single(items.Content)).Text);
        Assert.Contains("404", Assert.IsType<TextContentBlock>(Assert.Single(tested.Content)).Text);
        lock (_backend.Requests)
        {
            Assert.Contains(_backend.Requests, r => r.Method == "GET" && r.Path == "/v1/sources/src1/items");
            var test = Assert.Single(_backend.Requests, r => r.Path == "/v1/sources/test-connection");
            Assert.Equal("{\"adapterType\":\"RssFeed\",\"config\":{\"feedUrl\":\"https://x.test/rss\"}}", test.Body);
        }
    }

    /// <summary>BG-05: read the brand guide and replace its draft.</summary>
    [Fact]
    public async Task BrandGuide_ReadAndUpdateDraft_SendTheExpectedRequests()
    {
        _backend.MapJson("GET", "/v1/brand", """{"data":{"status":"Draft","version":0,"waitingForApproval":true,"brandPageUrl":"https://infoslides.app/app/brand"}}""");
        _backend.MapJson("PUT", "/v1/brand", """{"data":{"status":"Draft","waitingForApproval":true}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var read = await client.CallToolAsync("get_brand_guide", new Dictionary<string, object?>(), cancellationToken: Timeout());
        var updated = await client.CallToolAsync("update_brand_guide",
            new Dictionary<string, object?>
            {
                ["guide"] = new Dictionary<string, object?> { ["palette"] = new Dictionary<string, object?> { ["primary"] = "#0a5c36" } },
            }, cancellationToken: Timeout());

        Assert.Contains("waitingForApproval", Assert.IsType<TextContentBlock>(Assert.Single(read.Content)).Text);
        Assert.NotEqual(true, updated.IsError);
        lock (_backend.Requests)
        {
            var put = Assert.Single(_backend.Requests, r => r.Method == "PUT" && r.Path == "/v1/brand");
            Assert.Equal("{\"palette\":{\"primary\":\"#0a5c36\"}}", put.Body);
        }
    }

    /// <summary>A 204 with no body is a success, not "missing data payload".</summary>
    [Fact]
    public async Task DeleteScheduleEntry_NoContent_IsASuccess()
    {
        _backend.MapJson("DELETE", "/v1/devices/d1/schedule/entries/e1", "", 204);
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("delete_schedule_entry",
            new Dictionary<string, object?> { ["deviceId"] = "d1", ["entryId"] = "e1" }, cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("\"ok\":true", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
    }

    [Fact]
    public async Task UndoToken_ComesBackOnAChangeAndUndoChangeSendsIt()
    {
        _backend.MapJson("PATCH", "/v1/slides/sl1",
            """{"data":{"id":"sl1"},"undo":{"method":"POST","path":"/v1/undo","body":{"token":"tok1"}}}""");
        _backend.MapJson("POST", "/v1/undo", """{"data":{"restored":1}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var changed = await client.CallToolAsync("update_slide",
            new Dictionary<string, object?> { ["slideId"] = "sl1", ["hidden"] = true }, cancellationToken: Timeout());
        Assert.Contains("\"token\":\"tok1\"", Assert.IsType<TextContentBlock>(Assert.Single(changed.Content)).Text);

        var undone = await client.CallToolAsync("undo_change",
            new Dictionary<string, object?> { ["token"] = "tok1" }, cancellationToken: Timeout());
        Assert.NotEqual(true, undone.IsError);
        lock (_backend.Requests)
        {
            Assert.Contains("\"token\":\"tok1\"", _backend.Requests.Last().Body);
        }
    }

    [Fact]
    public async Task SetWorkspaceOfflineAlerts_NoQuietHoursSendsExplicitNull()
    {
        _backend.MapJson("PUT", "/v1/workspace/offline-alerts", """{"data":{"quietHours":null}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("set_workspace_offline_alerts", cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        lock (_backend.Requests)
        {
            Assert.Contains("\"quietHours\":null", Assert.Single(_backend.Requests).Body);
        }
    }

    [Fact]
    public async Task ShowOnScreen_NeedsExactlyOneOfFileOrUrl()
    {
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("show_media_on_device",
            new Dictionary<string, object?> { ["deviceId"] = "d1" }, cancellationToken: Timeout());

        Assert.True(result.IsError);
        lock (_backend.Requests)
        {
            Assert.Empty(_backend.Requests);
        }
    }

    [Fact]
    public async Task MakeAiSlide_PostsTheHandlerAndPrompt()
    {
        _backend.MapJson("POST", "/v1/slideshows/s1/slides/ai",
            """{"data":{"jobId":"j1","status":"Processing","statusUrl":"/v1/slideshows/s1/slides/ai/j1"}}""", 202);
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("make_ai_slide",
            new Dictionary<string, object?> { ["slideshowId"] = "s1", ["handler"] = "prompt", ["prompt"] = "Lunch 11-2" },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        lock (_backend.Requests)
        {
            var body = Assert.Single(_backend.Requests).Body;
            Assert.Contains("\"handler\":\"prompt\"", body);
            Assert.Contains("Lunch 11-2", body);
        }
    }

    [Fact]
    public async Task GetScreenNow_ReturnsThePngAsAnImage()
    {
        _backend.MapBytes("GET", "/v1/devices/d1/now.png", [0x89, 0x50, 0x4E, 0x47], "image/png");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("get_now_slide_png",
            new Dictionary<string, object?> { ["deviceId"] = "d1" }, cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        Assert.Contains(result.Content, c => c is ImageContentBlock);
    }

    /// <summary>The two play-time tools each call their own endpoint, named after its operationId.</summary>
    [Fact]
    public async Task PlayTimeTools_CallTheirOwnEndpoints()
    {
        _backend.MapJson("GET", "/v1/slideshows/s1/plays", """{"data":{}}""");
        _backend.MapJson("GET", "/v1/devices/d1/plays", """{"data":{}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        await client.CallToolAsync("get_slideshow_plays",
            new Dictionary<string, object?> { ["slideshowId"] = "s1", ["from"] = "2026-08-01", ["to"] = "2026-08-31" }, cancellationToken: Timeout());
        await client.CallToolAsync("get_device_plays",
            new Dictionary<string, object?> { ["deviceId"] = "d1", ["from"] = "2026-08-01", ["to"] = "2026-08-31" }, cancellationToken: Timeout());

        lock (_backend.Requests)
        {
            Assert.Collection(_backend.Requests,
                r => Assert.Equal("/v1/slideshows/s1/plays", r.Path),
                r => Assert.Equal("/v1/devices/d1/plays", r.Path));
        }
    }

    /// <summary>The feedback tools post to their own routes with the wire field names.</summary>
    [Fact]
    public async Task FeedbackTools_PostToTheirRoutes()
    {
        _backend.MapJson("POST", "/v1/feedback/issues", """{"data":{"id":"f1"}}""", 201);
        _backend.MapJson("POST", "/v1/feedback/testimonials", """{"data":{"id":"f2"}}""", 201);
        await using var client = await ConnectAsync("isk_admin_test");

        await client.CallToolAsync("report_issue",
            new Dictionary<string, object?> { ["problem"] = "No Google Slides import", ["agent"] = "Claude" }, cancellationToken: Timeout());
        await client.CallToolAsync("leave_testimonial",
            new Dictionary<string, object?> { ["quote"] = "Two calls.", ["context"] = "menu board" }, cancellationToken: Timeout());

        lock (_backend.Requests)
        {
            Assert.Collection(_backend.Requests,
                r => { Assert.Equal("/v1/feedback/issues", r.Path); Assert.Contains("\"problem\":\"No Google Slides import\"", r.Body); },
                r => { Assert.Equal("/v1/feedback/testimonials", r.Path); Assert.Contains("\"context\":\"menu board\"", r.Body); });
        }
    }

    /// <summary>update_device sends a location with the rest, and refuses half a coordinate before calling the API.</summary>
    [Fact]
    public async Task UpdateDevice_SendsLocation_AndNeedsBothCoordinates()
    {
        _backend.MapJson("PATCH", "/v1/devices/d1", """{"data":{}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var half = await client.CallToolAsync("update_device",
            new Dictionary<string, object?> { ["deviceId"] = "d1", ["latitude"] = 64.1 }, cancellationToken: Timeout());
        await client.CallToolAsync("update_device",
            new Dictionary<string, object?> { ["deviceId"] = "d1", ["name"] = "Lobby", ["latitude"] = 64.1, ["longitude"] = -21.9 }, cancellationToken: Timeout());

        Assert.True(half.IsError);
        lock (_backend.Requests)
        {
            var request = Assert.Single(_backend.Requests);
            Assert.Contains("\"name\":\"Lobby\"", request.Body);
            Assert.Contains("\"longitude\":-21.9", request.Body);
        }
    }

    [Fact]
    public async Task GetBilling_ReadsTheSummary_WithoutABody()
    {
        _backend.MapJson("GET", "/v1/billing",
            """{"data":{"plan":"Professional","status":"active","renewsAt":"2026-11-29T00:00:00Z","devices":{"used":2,"limit":5}}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("get_billing", new Dictionary<string, object?>(), cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("\"status\":\"active\"", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        lock (_backend.Requests)
        {
            var request = Assert.Single(_backend.Requests);
            Assert.Equal("GET", request.Method);
            Assert.Equal("/v1/billing", request.Path);
        }
    }

    [Fact]
    public async Task UpgradeSubscription_SendsPlanAndPeriod()
    {
        _backend.MapJson("POST", "/v1/billing/checkout",
            """{"data":{"checkoutUrl":"https://infoslides.app/checkout?_ptxn=txn_1","plan":"Starter","billingPeriod":"annual","price":{"amount":84.0,"currency":"EUR"}}}""");
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("upgrade_subscription",
            new Dictionary<string, object?> { ["plan"] = "Starter", ["billingPeriod"] = "annual" }, cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("\"amount\":84", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        lock (_backend.Requests)
        {
            Assert.Contains("\"billingPeriod\":\"annual\"", Assert.Single(_backend.Requests).Body);
        }
    }
}
