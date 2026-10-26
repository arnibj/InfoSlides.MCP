// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol.Server;
using InfoSlides.Core.Serialization;
using Ctx = InfoSlides.Core.Serialization.InfoSlidesJsonContext;

namespace InfoSlides.Mcp.Tools;

/// <summary>
/// The one table that says what each tool returns in the <c>data</c> of its result envelope, so every tool can
/// declare an <c>outputSchema</c>. Pass-through tools map to <c>JsonElement</c>, whose schema accepts any JSON: it
/// says what the API returns and no more.
/// </summary>
public static class ToolOutputSchemas
{
    /// <summary>Tool name to the type of its result <c>data</c>.</summary>
    public static readonly IReadOnlyDictionary<string, JsonTypeInfo> Results = new Dictionary<string, JsonTypeInfo>
    {
        ["create_api_key"] = Ctx.Default.CreateApiKeyResult,
        ["list_api_keys"] = Ctx.Default.ListApiKeyInfo,
        ["revoke_api_key"] = Ctx.Default.OkResult,
        ["get_billing"] = Ctx.Default.JsonElement,
        ["upgrade_subscription"] = Ctx.Default.JsonElement,
        ["create_device"] = Ctx.Default.Device,
        ["list_devices"] = Ctx.Default.ListDevice,
        ["get_device_status"] = Ctx.Default.DeviceStatus,
        ["assign_schedule"] = Ctx.Default.Schedule,
        ["get_stream_link"] = Ctx.Default.StreamLink,
        ["upload_media"] = Ctx.Default.UploadedMedia,
        ["get_upload_limits"] = Ctx.Default.JsonElement,
        ["list_media"] = Ctx.Default.JsonElement,
        ["get_media"] = Ctx.Default.JsonElement,
        ["update_media"] = Ctx.Default.JsonElement,
        ["delete_media"] = Ctx.Default.JsonElement,
        ["list_media_folders"] = Ctx.Default.JsonElement,
        ["create_media_folder"] = Ctx.Default.JsonElement,
        ["rename_media_folder"] = Ctx.Default.JsonElement,
        ["delete_media_folder"] = Ctx.Default.JsonElement,
        ["get_platform_version"] = Ctx.Default.JsonElement,
        ["get_platform_notices"] = Ctx.Default.JsonElement,
        ["search_articles"] = Ctx.Default.JsonElement,
        ["get_article"] = Ctx.Default.JsonElement,
        ["pair_device"] = Ctx.Default.JsonElement,
        ["play_slideshow_find_device"] = Ctx.Default.JsonElement,
        ["get_schedule"] = Ctx.Default.JsonElement,
        ["add_schedule_entry"] = Ctx.Default.JsonElement,
        ["delete_schedule_entry"] = Ctx.Default.JsonElement,
        ["update_schedule_entry"] = Ctx.Default.JsonElement,
        ["delete_device"] = Ctx.Default.JsonElement,
        ["create_broadcast_device"] = Ctx.Default.JsonElement,
        ["create_takeover"] = Ctx.Default.JsonElement,
        ["list_takeovers"] = Ctx.Default.JsonElement,
        ["end_takeover"] = Ctx.Default.JsonElement,
        ["get_slideshow_plays"] = Ctx.Default.JsonElement,
        ["get_device_plays"] = Ctx.Default.JsonElement,
        ["identify_devices"] = Ctx.Default.JsonElement,
        ["get_device_diagnosis"] = Ctx.Default.JsonElement,
        ["show_media_on_device"] = Ctx.Default.JsonElement,
        ["get_show_status"] = Ctx.Default.JsonElement,
        ["update_device"] = Ctx.Default.JsonElement,
        ["set_device_offline_alerts"] = Ctx.Default.JsonElement,
        ["upload_slideshow"] = Ctx.Default.Slideshow,
        ["update_slideshow"] = Ctx.Default.Slideshow,
        ["delete_slideshow"] = Ctx.Default.OkResult,
        ["restore_slideshow"] = Ctx.Default.JsonElement,
        ["import_google_slides"] = Ctx.Default.JsonElement,
        ["list_shared_slideshows"] = Ctx.Default.JsonElement,
        ["import_slide"] = Ctx.Default.JsonElement,
        ["replace_slideshow_file"] = Ctx.Default.Slideshow,
        ["list_slideshows"] = Ctx.Default.ListSlideshow,
        ["get_slideshow"] = Ctx.Default.Slideshow,
        ["clone_slideshow"] = Ctx.Default.Slideshow,
        ["list_gallery"] = Ctx.Default.ListGalleryItem,
        ["add_media_slide"] = Ctx.Default.Slide,
        ["add_designed_slide"] = Ctx.Default.DesignedSlide,
        ["get_slide_design"] = Ctx.Default.JsonElement,
        ["update_slide_design"] = Ctx.Default.JsonElement,
        ["search_stock_images"] = Ctx.Default.JsonElement,
        ["add_weather_alert_timeline"] = Ctx.Default.JsonElement,
        ["add_dynamic_slide"] = Ctx.Default.Slide,
        ["upload_pptx"] = Ctx.Default.Slideshow,
        ["set_slide_conditions"] = Ctx.Default.Slide,
        ["update_slide"] = Ctx.Default.Slide,
        ["delete_slide"] = Ctx.Default.OkResult,
        ["list_sources"] = Ctx.Default.ListSource,
        ["create_template"] = Ctx.Default.Template,
        ["list_templates"] = Ctx.Default.ListTemplate,
        ["list_template_categories"] = Ctx.Default.JsonElement,
        ["get_template"] = Ctx.Default.JsonElement,
        ["update_template"] = Ctx.Default.JsonElement,
        ["delete_template"] = Ctx.Default.JsonElement,
        ["clone_template"] = Ctx.Default.JsonElement,
        ["get_template_usage"] = Ctx.Default.JsonElement,
        ["regenerate_template_thumbnail"] = Ctx.Default.JsonElement,
        ["update_source"] = Ctx.Default.OkResult,
        ["push_data"] = Ctx.Default.PushReceived,
        ["get_source_status"] = Ctx.Default.PushSourceStatus,
        ["create_source_key"] = Ctx.Default.PushKey,
        ["create_tenant"] = Ctx.Default.CreateTenantResult,
        ["get_tenant_info"] = Ctx.Default.TenantInfo,
        ["resend_verification_email"] = Ctx.Default.OkResult,
        ["list_team"] = Ctx.Default.JsonElement,
        ["invite_team_member"] = Ctx.Default.JsonElement,
        ["revoke_team_invitation"] = Ctx.Default.JsonElement,
        ["remove_team_member"] = Ctx.Default.JsonElement,
        ["deactivate_member"] = Ctx.Default.JsonElement,
        ["reactivate_member"] = Ctx.Default.JsonElement,
        ["update_tenant"] = Ctx.Default.JsonElement,
        ["list_adapters"] = Ctx.Default.JsonElement,
        ["create_source"] = Ctx.Default.JsonElement,
        ["update_source_settings"] = Ctx.Default.JsonElement,
        ["delete_source"] = Ctx.Default.JsonElement,
        ["fetch_source_now"] = Ctx.Default.JsonElement,
        ["get_source_items"] = Ctx.Default.JsonElement,
        ["test_source_config"] = Ctx.Default.JsonElement,
        ["get_source_fields"] = Ctx.Default.JsonElement,
        ["get_audit_trail"] = Ctx.Default.JsonElement,
        ["get_error_log"] = Ctx.Default.JsonElement,
        ["get_workspace_health"] = Ctx.Default.JsonElement,
        ["get_offline_alerts"] = Ctx.Default.JsonElement,
        ["set_workspace_offline_alerts"] = Ctx.Default.JsonElement,
        ["undo_change"] = Ctx.Default.JsonElement,
        ["report_issue"] = Ctx.Default.JsonElement,
        ["leave_testimonial"] = Ctx.Default.JsonElement,
        ["make_ai_slide"] = Ctx.Default.JsonElement,
        ["get_ai_slide_job"] = Ctx.Default.JsonElement,
        ["insert_ai_slides"] = Ctx.Default.JsonElement,
        ["start_ai_studio_trial"] = Ctx.Default.JsonElement,
        ["get_brand_guide"] = Ctx.Default.JsonElement,
        ["update_brand_guide"] = Ctx.Default.JsonElement,
        ["list_brand_previews"] = Ctx.Default.JsonElement,
        ["upload_brand_logo"] = Ctx.Default.JsonElement,
        ["check_brand"] = Ctx.Default.JsonElement,
    };

    /// <summary>
    /// Tools that return no JSON envelope, on purpose: the four that return an image. The hosted-only
    /// <c>get_user_profile</c> lives in the host project and returns a bare object, so it is not listed here.
    /// </summary>
    public static readonly IReadOnlySet<string> Exempt = new HashSet<string> { "get_now_slide_png", "preview_slide", "preview_new_template", "preview_brand" };

    /// <summary>
    /// The results are written without null fields, so a field that may be null is never required, even when the
    /// model has no default for it (the exporter would otherwise require it).
    /// </summary>
    private static readonly JsonSchemaExporterOptions ExporterOptions = new()
    {
        TransformSchemaNode = (_, schema) =>
        {
            if (schema is JsonObject { } obj && obj["properties"] is JsonObject properties && obj["required"] is JsonArray required)
            {
                foreach (var name in required.Select(r => r!.GetValue<string>()).ToList())
                {
                    if (properties[name] is JsonObject { } property && property["type"] is JsonArray types && types.Any(t => t?.GetValue<string>() == "null"))
                    {
                        required.Remove(required.First(r => r!.GetValue<string>() == name));
                    }
                }

                if (required.Count == 0)
                {
                    obj.Remove("required");
                }
            }

            return schema;
        },
    };

    /// <summary>
    /// Builds the output schema of a tool: the result envelope, with <c>data</c> generated from the tool's own result
    /// type (source-generated metadata, so it is Native AOT safe). Only <c>data</c> is required; it may be null.
    /// The exporter emits no descriptions, which keeps the schemas inside the hosted directory rules.
    /// </summary>
    /// <param name="data">The metadata of the type the tool returns in <c>data</c>.</param>
    /// <returns>The schema as a JSON element.</returns>
    public static JsonElement SchemaFor(JsonTypeInfo data)
    {
        var dataSchema = JsonSchemaExporter.GetJsonSchemaAsNode(data, ExporterOptions);
        if (dataSchema is JsonObject { } obj && obj["type"] is JsonValue type && type.TryGetValue<string>(out var name) && name != "null")
        {
            obj["type"] = new JsonArray(name, "null");
        }

        RebaseRefs(dataSchema);
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["data"] = dataSchema,
                ["warnings"] = JsonSchemaExporter.GetJsonSchemaAsNode(Ctx.Default.ListApiWarning, ExporterOptions),
                ["undo"] = new JsonObject { ["type"] = "object" },
            },
            ["required"] = new JsonArray("data"),
        };
        return JsonDocument.Parse(schema.ToJsonString()).RootElement.Clone();
    }

    /// <summary>
    /// The exporter writes <c>$ref</c> pointers (for a type used twice, or for recursion) relative to the type's own
    /// schema; nested under <c>properties.data</c> they must point there instead.
    /// </summary>
    /// <param name="node">The exported schema, changed in place.</param>
    private static void RebaseRefs(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    if (key == "$ref" && value is JsonValue v && v.TryGetValue<string>(out var target) && target.StartsWith('#'))
                    {
                        obj[key] = "#/properties/data" + target[1..];
                    }
                    else
                    {
                        RebaseRefs(value);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    RebaseRefs(item);
                }

                break;
        }
    }

    /// <summary>
    /// Declares the output schema on every registered tool that has an entry in <see cref="Results"/>. Runs once, when the
    /// server's options are built, so the stdio and the hosted server list the same schemas.
    /// </summary>
    /// <param name="tools">The registered tools.</param>
    public static void Apply(IEnumerable<McpServerTool> tools)
    {
        foreach (var tool in tools)
        {
            if (Results.TryGetValue(tool.ProtocolTool.Name, out var data))
            {
                tool.ProtocolTool.OutputSchema = SchemaFor(data);
            }
        }
    }
}
