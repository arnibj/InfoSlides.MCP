using System.Text.Json;
using InfoSlides.Mcp.Tools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>
/// Shared set-up for the stdio smoke tests. They are split over several classes only so xunit runs the
/// classes in parallel (each test spawns its own server process). End-to-end tests: spawn the real server binary with `--mcp` (stdio transport), point it at a
/// fake InfoSlides backend, and drive it with the SDK's MCP client. This exercises tool
/// registration, schema generation, the shared API client, and stdout protocol hygiene at once.
/// </summary>
public abstract class McpStdioSmokeBase : IAsyncLifetime
{
    protected static readonly string[] ExpectedTools =
    [
        "create_tenant", "get_tenant_info", "resend_verification_email",
        "upload_slideshow", "upload_pptx", "update_slideshow", "list_slideshows", "get_slideshow",
        "clone_slideshow", "list_gallery", "add_media_slide", "add_designed_slide", "add_dynamic_slide", "upload_media", "set_slide_conditions", "preview_slide",
        "create_template", "list_templates", "update_source", "push_data", "get_source_status", "create_source_key",
        "create_device", "list_devices", "get_device_status", "assign_schedule", "get_stream_link",
        "create_api_key", "list_api_keys", "revoke_api_key",
        "upgrade_subscription", "get_billing",
        "update_slide", "delete_slide", "delete_slideshow", "replace_slideshow_file", "list_sources",
        "pair_device", "play_slideshow_find_device", "get_schedule", "add_schedule_entry", "delete_schedule_entry",
        "create_takeover", "list_takeovers", "end_takeover", "get_slideshow_plays", "get_device_plays",
        "list_team", "invite_team_member", "revoke_team_invitation", "remove_team_member", "update_tenant",
        "list_adapters", "create_source", "update_source_settings", "delete_source",
        "fetch_source_now", "get_source_items", "test_source_config",
        "get_brand_guide", "update_brand_guide", "preview_brand", "check_brand", "check_slideshow",
        "identify_devices", "get_now_slide_png", "get_device_diagnosis", "show_media_on_device", "get_show_status", "update_device",
        "set_device_offline_alerts", "get_workspace_health", "get_offline_alerts", "set_workspace_offline_alerts",
        "undo_change", "make_ai_slide", "get_ai_slide_job", "insert_ai_slides", "start_ai_studio_trial", "preview_new_template",
        "report_issue", "leave_testimonial",
        "get_platform_version", "get_platform_notices", "search_articles", "get_article",
        "get_upload_limits", "list_media", "get_media", "update_media", "delete_media",
        "list_media_folders", "create_media_folder", "rename_media_folder", "delete_media_folder",
        "list_template_categories", "get_template", "update_template", "delete_template", "clone_template",
        "get_template_usage", "regenerate_template_thumbnail",
        "get_source_fields", "get_audit_trail", "get_error_log", "deactivate_member", "reactivate_member",
        "list_brand_previews", "upload_brand_logo",
        "restore_slideshow", "import_google_slides", "list_shared_slideshows", "import_slide",
        "get_slide_design", "update_slide_design", "search_stock_images", "add_weather_alert_timeline",
        "update_schedule_entry", "delete_device", "create_broadcast_device",
    ];

    protected readonly FakeBackend _backend = new();
    protected readonly string _isolatedHome = Path.Combine(Path.GetTempPath(), "infoslides-mcp-tests-" + Guid.NewGuid().ToString("N"));

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
    protected static CancellationToken Timeout() => new CancellationTokenSource(TimeSpan.FromSeconds(120)).Token;

    protected async Task<McpClient> ConnectAsync(string? apiKey)
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
        // Always set, null included: a null value removes the variable from the child, so a key exported on the
        // machine running the tests cannot leak in and turn "no credential" into an authenticated call.
        environment["INFOSLIDES_API_KEY"] = apiKey;

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "infoslides-under-test",
            Command = "dotnet",
            Arguments = [serverDll, "--mcp"],
            EnvironmentVariables = environment,
        });

        return await McpClient.CreateAsync(transport, cancellationToken: Timeout());
    }
}
