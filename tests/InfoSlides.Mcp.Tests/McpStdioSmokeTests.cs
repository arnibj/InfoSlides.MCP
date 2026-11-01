using System.Text.Json;
using InfoSlides.Mcp.Tools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>Stdio smoke tests, part 1. See <see cref="McpStdioSmokeBase"/> for why they are split.</summary>
public sealed class McpStdioSmokeTests : McpStdioSmokeBase
{
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

    /// <summary>APX-58: every tool that returns the result envelope declares an output schema; the image tools do not.</summary>
    [Fact]
    public async Task EveryEnvelopeTool_DeclaresAnOutputSchema()
    {
        await using var client = await ConnectAsync("isk_admin_test");

        var tools = await client.ListToolsAsync(cancellationToken: Timeout());

        foreach (var tool in tools)
        {
            if (ToolOutputSchemas.Exempt.Contains(tool.Name))
            {
                Assert.Null(tool.ProtocolTool.OutputSchema);
                continue;
            }

            var schema = tool.ProtocolTool.OutputSchema;
            Assert.True(schema is not null, tool.Name);
            Assert.Equal("object", schema!.Value.GetProperty("type").GetString());
            Assert.Equal("data", Assert.Single(schema.Value.GetProperty("required").EnumerateArray()).GetString());
        }
    }

    /// <summary>APX-58: the same envelope arrives as structured content and as the text block.</summary>
    [Fact]
    public async Task ListSlideshows_ReturnsTheEnvelopeAsStructuredContentAndText()
    {
        _backend.MapJson("GET", "/v1/slideshows",
            """
            {"data":{"items":[{"id":"s1","title":"Menu","resolution":{"width":1920,"height":1080}}],"total":1,"page":1,"pageSize":50,"totalPages":1},
             "warnings":[{"code":"AspectMismatch","message":"ratio differs"}]}
            """);
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("list_slideshows", cancellationToken: Timeout());

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.NotNull(result.StructuredContent);
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(text).RootElement, result.StructuredContent!.Value));
        Assert.Equal("s1", result.StructuredContent!.Value.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetString());
    }

    /// <summary>APX-58: an error result carries no structured content.</summary>
    [Fact]
    public async Task ErrorResult_HasNoStructuredContent()
    {
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("update_slide",
            new Dictionary<string, object?> { ["slideId"] = "sl1" }, cancellationToken: Timeout());

        Assert.True(result.IsError);
        Assert.Null(result.StructuredContent);
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
    [InlineData("delete_slideshow", new[] { "restore_slideshow", "confirm" })]
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
}
