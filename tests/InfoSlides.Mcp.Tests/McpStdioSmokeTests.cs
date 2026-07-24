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
    public async Task CreateTenant_WorksAnonymously()
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
