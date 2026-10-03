using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using InfoSlides.Mcp.Tools;
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

        var jsonOptions = InfoSlidesToolRegistration.CreateJsonOptions();

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
                options.ServerInstructions = InfoSlidesServerInstructions.Full + updateNotice;
            })
            .WithStdioServerTransport()
            .WithInfoSlidesTools(jsonOptions);

        await builder.Build().RunAsync(ct).ConfigureAwait(false);
        return 0;
    }
}
