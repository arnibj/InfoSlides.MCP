// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Json.Schema;
using InfoSlides.Core.Api;
using InfoSlides.Core.Models;
using InfoSlides.Core.Serialization;
using InfoSlides.Mcp.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>APX-58: every envelope tool declares an output schema that its real results satisfy.</summary>
public sealed class ToolOutputSchemaTests
{
    private const string SlideJson =
        """{"id":"sl1","mediaUrl":null,"templateId":"t1","durationSeconds":10,"position":0,"conditions":null,"sourceId":null,"pushKey":null,"type":"dynamic","hidden":null,"thumbnailUrl":null,"templateName":null,"rules":[{"mode":"ShowOnly","match":"All","conditions":[{"type":"date","value":"2026-12-24"}],"readOnly":false,"summary":"Dec 24"}]}""";

    private const string SlideshowJson =
        """{"id":"s1","title":"Menu","resolution":{"width":1920,"height":1080},"slides":[""" + SlideJson +
        """],"playbackModeOverride":null,"effectivePlaybackMode":"VideoStream","defaultDurationSeconds":null,"ticker":{"enabled":false,"sourceIds":null},"clock":{"enabled":true,"position":"TopRight","showDate":null,"backgroundColor":null,"textColor":null},"shared":null,"renderStatus":"Pending","lastRenderedAt":null,"screens":[{"deviceId":"d1","name":"Front lobby","playerUrl":"https://infoslides.app/player/tok1"}],"screenCount":1}""";

    private const string UndoJson = """{"method":"POST","path":"/v1/undo","body":{"token":"abc"}}""";

    /// <summary>One API payload per typed result, shaped like the real API, with null for nullable and optional fields.</summary>
    private static readonly Dictionary<Type, string> Samples = new()
    {
        [typeof(CreateApiKeyResult)] = """{"id":"k1","key":"isk_admin_abc","type":"admin","name":"Lobby"}""",
        [typeof(List<ApiKeyInfo>)] = """[{"id":"k1","name":"Lobby","type":"slide","prefix":"isk_slide_ab","slideIds":null,"createdAt":"2026-10-01T09:00:00+00:00","lastUsedAt":null,"revokedAt":null}]""",
        [typeof(OkResult)] = "{}",
        [typeof(Device)] = """{"id":"d1","name":"Front lobby","resolution":{"width":1920,"height":1080},"nowPlayingSlideshowId":null,"nowPlayingTitle":null,"supportsHtml":false,"distanceMeters":null}""",
        [typeof(List<Device>)] = """[{"id":"d1","name":"Front lobby","resolution":{"width":1920,"height":1080},"nowPlayingSlideshowId":"s1","nowPlayingTitle":"Menu","supportsHtml":true,"distanceMeters":12.5}]""",
        [typeof(DeviceStatus)] = """{"online":false,"lastSeenAt":null,"nowPlaying":null}""",
        [typeof(Schedule)] = """{"deviceId":"d1","slideshowIds":["s1","s2"]}""",
        [typeof(StreamLink)] = """{"hlsUrl":null,"expiresAt":null,"playerUrl":"https://infoslides.app/player/tok1","playbackMode":"Html"}""",
        [typeof(UploadedMedia)] = """{"id":"m1","fileType":"image","width":null,"height":null}""",
        [typeof(Slideshow)] = SlideshowJson,
        [typeof(SlideshowPage)] = """{"items":[""" + SlideshowJson + """],"total":1,"page":1,"pageSize":50,"totalPages":1}""",
        [typeof(List<GalleryItem>)] = """[{"id":"g1","title":"Opening hours","description":null,"previewUrl":null,"resolution":null}]""",
        [typeof(Slide)] = SlideJson,
        [typeof(DesignedSlide)] = """{"id":"sl1","designId":"des1","layout":"Hero","previewUrl":null,"durationSeconds":8,"position":0}""",
        [typeof(List<Source>)] = """[{"id":"src1","name":"News","adapterType":"Rss","lastFetchedAt":null,"isEnabled":true,"health":null,"lastError":null,"consecutiveFailures":0,"lastSuccessAt":null}]""",
        [typeof(Template)] = """{"id":"t1","title":"Specials","sampleJson":null,"html":null,"css":null}""",
        [typeof(List<Template>)] = """[{"id":"t1","title":"Specials","sampleJson":{"price":"1.990 kr"},"html":"<div></div>","css":"div{}"}]""",
        [typeof(PushReceived)] = """{"receivedAt":null}""",
        [typeof(PushSourceStatus)] = """{"id":"src1","name":"Gym","lastReceivedAt":null,"hideAfterMinutes":null,"isShowingData":false,"slideIds":["sl1"],"adapterType":null,"lastFetchedAt":null,"isEnabled":true,"health":null,"lastError":null,"consecutiveFailures":0,"lastSuccessAt":null}""",
        [typeof(PushKey)] = """{"id":"pk1","name":"Gym","keyPrefix":"isk_push_ab","key":"isk_push_abc","quickUpdateUrl":null}""",
        [typeof(CreateTenantResult)] = """{"tenantId":"ten1","apiKey":"isk_admin_abc","verificationEmailSent":true}""",
        [typeof(TenantInfo)] = """{"tenantId":"ten1","name":"Cafe","ownerEmail":"a@b.is","isEmailVerified":false,"subscriptionLevel":"Free","deviceQuota":{"used":0,"max":1},"keyScope":null}""",
    };

    /// <summary>Every registered tool is in the table or exempt, and nothing in either names a missing tool.</summary>
    [Fact]
    public void Table_CoversEveryRegisteredTool_AndNothingElse()
    {
        var registered = typeof(TenantTools).Assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .Where(n => n != null)
            .Select(n => n!)
            .ToHashSet();
        var listed = ToolOutputSchemas.Results.Keys.Concat(ToolOutputSchemas.Exempt).ToList();

        Assert.Empty(registered.Except(listed));
        Assert.Empty(listed.Except(registered));
        Assert.Empty(ToolOutputSchemas.Results.Keys.Intersect(ToolOutputSchemas.Exempt));
    }

    /// <summary>
    /// A real result for every typed tool, built by <c>ToolResults.Execute</c> from a stubbed API payload, satisfies that
    /// tool's schema, as does the payload itself; a schema stricter than the data would fail here. Pass-through tools
    /// return raw API JSON and their <c>data</c> schema accepts any value, so there is nothing to test per tool.
    /// </summary>
    [Fact]
    public async Task EveryTypedResult_SatisfiesItsOutputSchema()
    {
        var run = typeof(ToolOutputSchemaTests).GetMethod(nameof(RunAsync), BindingFlags.NonPublic | BindingFlags.Static)!;
        var checkedTools = 0;

        foreach (var (tool, info) in ToolOutputSchemas.Results.Where(r => r.Value.Type != typeof(JsonElement)))
        {
            Assert.True(Samples.TryGetValue(info.Type, out var sample), $"{tool}: add a sample for {info.Type}");
            var schema = JsonSchema.FromText(ToolOutputSchemas.SchemaFor(info).GetRawText());
            var result = await (Task<CallToolResult>)run.MakeGenericMethod(info.Type).Invoke(null, [sample, info])!;

            Assert.NotEqual(true, result.IsError);
            Assert.True(schema.Evaluate(JsonNode.Parse(result.StructuredContent!.Value.GetRawText())).IsValid, $"{tool}: result does not match its schema");
            Assert.True(schema.Evaluate(JsonNode.Parse("{\"data\":" + sample + "}")).IsValid, $"{tool}: API payload does not match its schema");
            checkedTools++;
        }

        Assert.True(checkedTools > 20);
    }

    private static Task<CallToolResult> RunAsync<T>(string sample, JsonTypeInfo<T> info)
    {
        var data = JsonSerializer.Deserialize(sample, info)!;
        var warnings = new[] { new ApiWarning("AspectMismatch", "ratio differs") };
        return ToolResults.Execute(
            () => Task.FromResult(new ApiResult<T>(data, warnings, JsonDocument.Parse(UndoJson).RootElement)), info);
    }

    /// <summary>The data may be null, and the schemas hold no description text.</summary>
    [Fact]
    public void Schemas_AllowNullData_AndCarryNoDescriptions()
    {
        foreach (var (tool, info) in ToolOutputSchemas.Results)
        {
            var text = ToolOutputSchemas.SchemaFor(info).GetRawText();
            Assert.False(HasDescriptionKeyword(JsonNode.Parse(text)), tool);
            Assert.True(JsonSchema.FromText(text).Evaluate(JsonNode.Parse("""{"data":null}""")).IsValid, tool);
        }
    }

    /// <summary>True when a schema keyword (not a field called "description") named description is present.</summary>
    private static bool HasDescriptionKeyword(JsonNode? node) => node switch
    {
        JsonObject obj => obj.Any(p => (p.Key == "description" && p.Value is JsonValue) ||
                                       (p.Key == "properties" ? p.Value is JsonObject props && props.Any(f => HasDescriptionKeyword(f.Value)) : HasDescriptionKeyword(p.Value))),
        JsonArray array => array.Any(HasDescriptionKeyword),
        _ => false,
    };

    /// <summary>An error result is flagged and carries no structured content.</summary>
    [Fact]
    public async Task Error_HasNoStructuredContent()
    {
        var result = await ToolResults.Execute(
            Task<ApiResult<OkResult>> () => throw new MissingCredentialException(), InfoSlidesJsonContext.Default.OkResult);

        Assert.True(result.IsError);
        Assert.Null(result.StructuredContent);
    }
}
