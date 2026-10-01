// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using InfoSlides.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace InfoSlides.Mcp.Tools;

/// <summary>
/// The one place that lists the InfoSlides MCP tool classes, so the local stdio server and the hosted HTTP server
/// expose exactly the same tools. Tools are registered with the AOT-safe <c>WithTools&lt;T&gt;</c> path, never
/// <c>WithToolsFromAssembly</c>, whose assembly scanning breaks under trimming.
/// </summary>
public static class InfoSlidesToolRegistration
{
    /// <summary>
    /// Serializer options that know both the MCP protocol types and the InfoSlides API contract
    /// (<see cref="InfoSlidesJsonContext"/>), as every tool result is serialised with them.
    /// </summary>
    /// <returns>The combined options.</returns>
    public static JsonSerializerOptions CreateJsonOptions() => new(McpJsonUtilities.DefaultOptions)
    {
        TypeInfoResolver = JsonTypeInfoResolver.Combine(
            InfoSlidesJsonContext.Default,
            McpJsonUtilities.DefaultOptions.TypeInfoResolver),
    };

    /// <summary>Registers every InfoSlides tool class on the MCP server builder.</summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="jsonOptions">Options from <see cref="CreateJsonOptions"/>.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IMcpServerBuilder WithInfoSlidesTools(this IMcpServerBuilder builder, JsonSerializerOptions jsonOptions) => builder
        .WithTools<TenantTools>(jsonOptions)
        .WithTools<SlideshowTools>(jsonOptions)
        .WithTools<MediaTools>(jsonOptions)
        .WithTools<TemplateTools>(jsonOptions)
        .WithTools<DeviceTools>(jsonOptions)
        .WithTools<ApiKeyTools>(jsonOptions)
        .WithTools<BillingTools>(jsonOptions)
        .WithTools<ScreenTools>(jsonOptions)
        .WithTools<WorkspaceTools>(jsonOptions);
}
