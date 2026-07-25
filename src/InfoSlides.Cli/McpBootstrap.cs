using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using InfoSlides.Cli.Tools;
using InfoSlides.Core.Api;
using InfoSlides.Core.Config;
using InfoSlides.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace InfoSlides.Cli;

internal static class McpBootstrap
{
    /// <summary>
    /// Runs the MCP server over stdio. stdout carries the protocol, so every log line must go to
    /// stderr. Tools are registered with the AOT-safe WithTools&lt;T&gt; path — never
    /// WithToolsFromAssembly, whose assembly scanning breaks under trimming.
    /// </summary>
    public static async Task<int> RunAsync(string? apiKey, string? apiUrl, CancellationToken ct = default)
    {
        var settings = AppSettings.Resolve(apiKey, apiUrl);

        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

        var jsonOptions = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                InfoSlidesJsonContext.Default,
                McpJsonUtilities.DefaultOptions.TypeInfoResolver),
        };

        builder.Services.AddSingleton(new InfoSlidesApiClient(new HttpClient(), settings.ApiUrl, settings.Credential));
        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "infoslides",
                    Title = "InfoSlides digital signage",
                    Version = VersionInfo.Version,
                };
                options.ServerInstructions =
                    "Manage InfoSlides digital signage: tenants, slideshows, templates, devices and live " +
                    "HLS streams. New users: create_tenant returns the admin API key. Check get_tenant_info " +
                    "for entitlements. Watch for warnings (e.g. AspectMismatch) in results and self-correct. " +
                    "Errors may include an upgradeUrl when a Premium subscription is required.";
            })
            .WithStdioServerTransport()
            .WithTools<TenantTools>(jsonOptions)
            .WithTools<SlideshowTools>(jsonOptions)
            .WithTools<MediaTools>(jsonOptions)
            .WithTools<TemplateTools>(jsonOptions)
            .WithTools<DeviceTools>(jsonOptions)
            .WithTools<ApiKeyTools>(jsonOptions)
            .WithTools<BillingTools>(jsonOptions);

        await builder.Build().RunAsync(ct).ConfigureAwait(false);
        return 0;
    }
}
