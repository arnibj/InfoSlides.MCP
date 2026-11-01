using System.Text.Json;
using InfoSlides.Mcp.Tools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>Stdio smoke tests, part 6. See <see cref="McpStdioSmokeBase"/> for why they are split.</summary>
public sealed class McpStdioSmokeTests6 : McpStdioSmokeBase
{
    /// <summary>
    /// The 1.8.0 pass-through tools: each sends the route and body the API catalogue documents. The query string is
    /// not part of <see cref="FakeBackend"/>'s recorded path, so query parameters are covered by the body-less cases.
    /// </summary>
    public static TheoryData<string, string, string, string, string> PassThroughTools => new()
    {
        { "get_platform_version", "{}", "GET", "/v1/platform/version", "" },
        { "get_platform_notices", "{}", "GET", "/v1/platform/notices", "" },
        { "search_articles", """{"q":"portrait","type":"help"}""", "GET", "/v1/articles", "" },
        { "get_article", """{"type":"release-note","slug":"also-in-1-10-2"}""", "GET", "/v1/articles/release-note/also-in-1-10-2", "" },
        { "get_upload_limits", "{}", "GET", "/v1/media/limits", "" },
        { "list_media", """{"type":"image"}""", "GET", "/v1/media", "" },
        { "get_media", """{"mediaAssetId":"m1"}""", "GET", "/v1/media/m1", "" },
        { "update_media", """{"mediaAssetId":"m1","name":"Menu","shared":true}""", "PATCH", "/v1/media/m1", """{"name":"Menu","shared":true}""" },
        { "delete_media", """{"mediaAssetId":"m1"}""", "DELETE", "/v1/media/m1", "" },
        { "list_media_folders", "{}", "GET", "/v1/media/folders", "" },
        { "create_media_folder", """{"name":"Menus","parentFolderId":"f0"}""", "POST", "/v1/media/folders", """{"name":"Menus","parentFolderId":"f0"}""" },
        { "rename_media_folder", """{"folderId":"f1","name":"Specials"}""", "PATCH", "/v1/media/folders/f1", """{"name":"Specials"}""" },
        { "delete_media_folder", """{"folderId":"f1"}""", "DELETE", "/v1/media/folders/f1", "" },
        { "list_template_categories", "{}", "GET", "/v1/templates/categories", "" },
        { "get_template", """{"templateId":"t1"}""", "GET", "/v1/templates/t1", "" },
        { "update_template", """{"templateId":"t1","title":"Sales"}""", "PATCH", "/v1/templates/t1", """{"title":"Sales"}""" },
        { "delete_template", """{"templateId":"t1"}""", "DELETE", "/v1/templates/t1", "" },
        { "clone_template", """{"templateId":"t1"}""", "POST", "/v1/templates/t1/clone", "" },
        { "get_template_usage", """{"templateId":"t1"}""", "GET", "/v1/templates/t1/usage", "" },
        { "regenerate_template_thumbnail", """{"templateId":"t1"}""", "POST", "/v1/templates/t1/thumbnail", "" },
        { "get_source_fields", """{"sourceId":"src1"}""", "GET", "/v1/sources/src1/fields", "" },
        { "get_audit_trail", """{"action":"SlideshowDeleted"}""", "GET", "/v1/audit", "" },
        { "get_error_log", """{"level":"Error"}""", "GET", "/v1/errors", "" },
        { "deactivate_member", """{"memberId":"u1"}""", "POST", "/v1/team/members/u1/deactivate", "" },
        { "reactivate_member", """{"memberId":"u1"}""", "POST", "/v1/team/members/u1/reactivate", "" },
        { "list_brand_previews", "{}", "GET", "/v1/brand/preview", "" },
        { "restore_slideshow", """{"slideshowId":"s1"}""", "POST", "/v1/slideshows/s1/undelete", "" },
        { "check_slideshow", """{"slideshowId":"s1"}""", "GET", "/v1/slideshows/s1/preflight", "" },
        { "import_google_slides", """{"accessToken":"ya29.x","fileId":"g1"}""", "POST", "/v1/slideshows/import/google",
            """{"fileId":"g1","accessToken":"ya29.x"}""" },
        { "list_shared_slideshows", """{"excludeId":"s1"}""", "GET", "/v1/slideshows/shared", "" },
        { "import_slide", """{"slideshowId":"s1","sourceSlideshowId":"s2","slideNumber":3}""", "POST", "/v1/slideshows/s1/slides/import",
            """{"sourceSlideshowId":"s2","slideNumber":3}""" },
        { "get_slide_design", """{"slideId":"sl1"}""", "GET", "/v1/slides/sl1/design", "" },
        { "update_slide_design", """{"slideId":"sl1","design":{"heading":"Lunch at 1pm"}}""", "PUT", "/v1/slides/sl1/design", """{"heading":"Lunch at 1pm"}""" },
        { "search_stock_images", """{"q":"lake"}""", "GET", "/v1/stock-images", "" },
        { "add_weather_alert_timeline", """{"slideshowId":"s1","sourceId":"w1"}""", "POST", "/v1/slideshows/s1/slides/dynamic/weather-alert-timeline",
            """{"sourceId":"w1"}""" },
        { "update_schedule_entry", """{"deviceId":"d1","entryId":"e1","priority":2}""", "PATCH", "/v1/devices/d1/schedule/entries/e1", """{"priority":2}""" },
        { "delete_device", """{"deviceId":"d1"}""", "DELETE", "/v1/devices/d1", "" },
        { "create_broadcast_device", """{"name":"Lobby channel"}""", "POST", "/v1/devices/broadcast", """{"name":"Lobby channel"}""" },
    };

    [Fact]
    public async Task PassThroughTools_SendTheDocumentedRequest()
    {
        await using var client = await ConnectAsync("isk_admin_test");

        foreach (var row in PassThroughTools)
        {
            var (tool, argsJson, method, path, body) = ((string)row[0], (string)row[1], (string)row[2], (string)row[3], (string)row[4]);
            lock (_backend.Requests)
            {
                _backend.Requests.Clear();
            }

            _backend.MapJson(method, path, """{"data":{"ok":true}}""");
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argsJson)!
                .ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

            var result = await client.CallToolAsync(tool, args, cancellationToken: Timeout());

            Assert.True(result.IsError != true, tool);
            lock (_backend.Requests)
            {
                Assert.True(_backend.Requests.Count == 1, $"{tool} made {_backend.Requests.Count} requests");
                var request = _backend.Requests[0];
                Assert.True(method == request.Method && path == request.Path && body == request.Body,
                    $"{tool}: expected {method} {path} {body} but got {request.Method} {request.Path} {request.Body}");
            }
        }
    }

    [Fact]
    public async Task UpdateMedia_WithNothingToChange_IsRefusedWithoutACall()
    {
        await using var client = await ConnectAsync("isk_admin_test");

        var result = await client.CallToolAsync("update_media", new Dictionary<string, object?> { ["mediaAssetId"] = "m1" }, cancellationToken: Timeout());

        Assert.True(result.IsError);
        Assert.Contains("ValidationFailed", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        lock (_backend.Requests)
        {
            Assert.Empty(_backend.Requests);
        }
    }
}
