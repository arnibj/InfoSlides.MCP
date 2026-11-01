using System.Text.Json;
using InfoSlides.Mcp.Tools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>Stdio smoke tests, part 2. See <see cref="McpStdioSmokeBase"/> for why they are split.</summary>
public sealed class McpStdioSmokeTests2 : McpStdioSmokeBase
{
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
            {"data":{"items":[{"id":"s1","title":"Menu","resolution":{"width":1920,"height":1080}}],"total":1,"page":1,"pageSize":50,"totalPages":1},
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

        foreach (var name in new[]
                 {
                     "delete_slide", "delete_slideshow", "replace_slideshow_file", "delete_schedule_entry",
                     "end_takeover", "revoke_team_invitation", "remove_team_member", "delete_source",
                 })
        {
            Assert.True(tools.Single(t => t.Name == name).ProtocolTool.Annotations?.DestructiveHint, name);
        }

        Assert.True(tools.Single(t => t.Name == "list_sources").ProtocolTool.Annotations?.ReadOnlyHint);
    }

    /// <summary>A pass-through tool sends only the arguments given, and returns the API's data unchanged.</summary>
    [Fact]
    public async Task PairScreen_SendsTheGivenFields_AndReturnsTheApisData()
    {
        _backend.MapJson("POST", "/v1/pairings",
            """{"data":{"deviceId":"d1","deviceName":"Lobby TV","playerUrl":"https://infoslides.app/player/x","newField":1}}""", 201);
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("pair_device",
            new Dictionary<string, object?> { ["nickname"] = "swift-oak-42", ["slideshowId"] = "s1" },
            cancellationToken: Timeout());

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("\"newField\":1", text);
        lock (_backend.Requests)
        {
            var body = JsonDocument.Parse(Assert.Single(_backend.Requests).Body).RootElement;
            Assert.Equal(["nickname", "slideshowId"], body.EnumerateObject().Select(p => p.Name).ToArray());
        }
    }

    /// <summary>NeedsClarification carries its choices in details; the tool error must pass them on.</summary>
    [Fact]
    public async Task PlayNow_NeedsClarification_PassesTheChoicesOn()
    {
        _backend.MapJson("POST", "/v1/play",
            """{"error":{"code":"NeedsClarification","message":"Which screen?","details":{"question":"Which screen?","choices":[{"id":"d1","label":"Lobby","recommended":false}]}}}""", 409);
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("play_slideshow_find_device",
            new Dictionary<string, object?> { ["slideshowId"] = "s1" }, cancellationToken: Timeout());

        Assert.True(result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("\"choices\"", text);
        Assert.Contains("\"label\":\"Lobby\"", text);
    }
}
