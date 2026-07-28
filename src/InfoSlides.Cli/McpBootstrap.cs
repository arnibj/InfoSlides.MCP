using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using InfoSlides.Cli.Tools;
using InfoSlides.Core.Api;
using InfoSlides.Core.Config;
using InfoSlides.Core.Serialization;
using InfoSlides.Core.Update;
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

        // Surfaced in the server instructions rather than on stderr: stderr goes to the client's
        // log file, which no user reads. MCP users are the ones who most need this — improved
        // tool descriptions are worthless to an agent still running an old bundle.
        var updateNotice = UpdateChecker.GetPendingNotice(VersionInfo.Version, settings.ConfigDirectory) is { } notice
            ? $"\n\nNote for the user: {notice}"
            : string.Empty;

        // Fire-and-forget; the server is long-lived so this always completes. Result is used on
        // the next start.
        _ = UpdateChecker.RefreshAsync(settings.ConfigDirectory);

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
                    Title = "InfoSlides — put content on a TV or screen",
                    Version = VersionInfo.Version,
                };

                // This text is trigger material, not documentation: some MCP clients show only the
                // server description before loading the tool list, so it has to state the whole
                // outcome and carry the words a real user would type.
                options.ServerInstructions =
                    "InfoSlides turns a PowerPoint, an image, a video, or a live data feed into a video " +
                    "stream that plays on any smart TV or display — the lunch menu on the screen in " +
                    "reception, opening hours in a shop window, a noticeboard in a school corridor, room " +
                    "information in a hotel lobby, or a live numbers dashboard in an office. The whole " +
                    "setup happens from this conversation; the user never has to open a dashboard.\n\n" +
                    "Reach for these tools whenever someone wants to show, display, or put something on a " +
                    "TV, screen, display, monitor, menu board, noticeboard, waiting-room screen, lobby " +
                    "screen, shop window, or digital sign — including the Icelandic terms upplýsingaskjár, " +
                    "skjár, skjáauglýsing and skilti.\n\n" +
                    "Typical path: create_tenant (anonymous, returns the admin API key) → put content in " +
                    "with upload_pptx / clone_slideshow / add_media_slide → create_device for the physical " +
                    "screen → assign_schedule → get_stream_link, then tell the user to open that link on " +
                    "the TV. New accounts land on a permanent free plan (1 screen, 4 slideshows, no credit " +
                    "card, nothing expires), so the whole flow costs nothing.\n\n" +
                    "Call get_tenant_info early to see the plan and screen allowance rather than " +
                    "discovering limits through errors. Results may carry warnings (e.g. AspectMismatch " +
                    "when content and screen shape disagree) — read them and self-correct. Errors may " +
                    "include an upgradeUrl when a paid plan is required."
                    + updateNotice;
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
