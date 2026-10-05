// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using InfoSlides.Mcp.Tools;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InfoSlides.McpServer;

/// <summary>
/// Request filters that tailor the shared tool set to the caller: the hosted profile is an allow-list, location
/// parameters are hidden from hosted callers, local-file paths are hidden from everyone, every tool advertises the OAuth scope it needs, the profile tool is
/// marked for ChatGPT's multi-account support, and the list carries its caching hints. The SDK's tool objects are
/// shared between requests, so every tool is copied before it is changed.
/// </summary>
public static class ToolProfileFilters
{
    /// <summary>How long a client may cache a tool list.</summary>
    public static readonly TimeSpan ToolListTimeToLive = TimeSpan.FromHours(1);

    private const string ReadScope = "infoslides.read";
    private const string WriteScope = "infoslides.write";
    private const string ProfileToolName = "get_user_profile";

    /// <summary>
    /// The shared tools take a <c>filePath</c> on the caller's own machine. On this host that path would name a file on
    /// the server, so the parameter is removed for every caller, tools that need it are not offered, and a call that
    /// sends it is refused before the file is touched.
    /// </summary>
    public const string ServerFileParameter = "filePath";

    /// <summary>Adds the list-tools and call-tool filters to the MCP server builder.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The same builder.</returns>
    public static IMcpServerBuilder WithProfileFilters(this IMcpServerBuilder builder) =>
        builder.WithRequestFilters(filters =>
        {
            filters.AddListToolsFilter(next => async (context, ct) =>
            {
                var result = await next(context, ct);
                var user = UserOf(context.Services);
                var profile = CallerContext.ProfileFor(user);
                var isApiKey = CallerContext.IsApiKey(user);
                var surface = CallerContext.Surface(user);

                result.Tools = result.Tools
                    .Where(t => profile == ToolProfile.Full || HostedToolProfile.IsOffered(t.Name, surface))
                    .Where(t => !isApiKey || t.Name != ProfileToolName)
                    .Where(t => !RequiresServerFile(t))
                    .Select(t => Decorate(t, profile))
                    .ToList();

                // The list depends on who is asking, so it is private to that caller.
                result.TimeToLive = ToolListTimeToLive;
                result.CacheScope = CacheScope.Private;
                return result;
            });

            filters.AddCallToolFilter(next => async (context, ct) =>
            {
                var user = UserOf(context.Services);
                var profile = CallerContext.ProfileFor(user);
                var name = context.Params?.Name ?? string.Empty;

                if (context.Params?.Arguments?.ContainsKey(ServerFileParameter) == true)
                {
                    return Refuse($"The parameter '{ServerFileParameter}' is not available here: this server cannot read your files. Use mediaUrl with a public address instead.");
                }

                if (profile == ToolProfile.Hosted && !HostedToolProfile.IsOffered(name, CallerContext.Surface(user)))
                {
                    return Refuse($"The tool '{name}' is not available here.");
                }

                if (CallerContext.IsApiKey(user) && name == ProfileToolName)
                {
                    return Refuse("The profile tool needs an OAuth connection.");
                }

                if (profile == ToolProfile.Hosted &&
                    HostedToolProfile.HiddenParameters.TryGetValue(name, out var hidden) &&
                    context.Params?.Arguments is { } arguments &&
                    hidden.FirstOrDefault(arguments.ContainsKey) is { } offending)
                {
                    return Refuse($"The parameter '{offending}' is not available here.");
                }

                return await next(context, ct);
            });
        });

    /// <summary>The server instructions for a caller, so a host that cannot load the skill still gets its guidance.</summary>
    /// <param name="user">The authenticated caller.</param>
    /// <returns>The instruction text for the caller's profile.</returns>
    public static string InstructionsFor(ClaimsPrincipal? user) => InfoSlidesServerInstructions.For(CallerContext.ProfileFor(user), CallerContext.Surface(user));

    private static ClaimsPrincipal? UserOf(IServiceProvider? services) =>
        services?.GetService<IHttpContextAccessor>()?.HttpContext?.User;

    private static CallToolResult Refuse(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }],
    };

    /// <summary>Whether a tool cannot work without a <see cref="ServerFileParameter"/>.</summary>
    /// <param name="tool">The tool definition.</param>
    /// <returns>True when <c>filePath</c> is a required parameter.</returns>
    private static bool RequiresServerFile(Tool tool) =>
        tool.InputSchema.TryGetProperty("required", out var required) &&
        required.ValueKind == JsonValueKind.Array &&
        required.EnumerateArray().Any(r => r.ValueKind == JsonValueKind.String && r.GetString() == ServerFileParameter);

    private static Tool Decorate(Tool shared, ToolProfile profile)
    {
        // Copy first: the SDK hands out the same Tool instances to every request.
        var tool = JsonSerializer.Deserialize<Tool>(
            JsonSerializer.SerializeToUtf8Bytes(shared, McpJsonUtilities.DefaultOptions),
            McpJsonUtilities.DefaultOptions)!;

        if (profile == ToolProfile.Hosted && HostedToolProfile.DescriptionOverrides.TryGetValue(tool.Name, out var description))
        {
            tool.Description = description;
        }

        var hidden = new HashSet<string>(StringComparer.Ordinal) { ServerFileParameter };
        if (profile == ToolProfile.Hosted && HostedToolProfile.HiddenParameters.TryGetValue(tool.Name, out var hostedHidden))
        {
            hidden.UnionWith(hostedHidden);
        }

        var schema = JsonNode.Parse(tool.InputSchema.GetRawText())!.AsObject();
        if (schema["properties"] is JsonObject properties)
        {
            foreach (var parameter in hidden)
            {
                properties.Remove(parameter);
            }

            if (profile == ToolProfile.Hosted && HostedToolProfile.ParameterDescriptionOverrides.TryGetValue(tool.Name, out var reworded))
            {
                foreach (var (parameter, text) in reworded)
                {
                    if (properties[parameter] is JsonObject property)
                    {
                        property["description"] = text;
                    }
                }
            }
        }

        if (schema["required"] is JsonArray required)
        {
            foreach (var node in required.Where(n => n is not null && hidden.Contains(n.GetValue<string>())).ToList())
            {
                required.Remove(node);
            }
        }

        tool.InputSchema = JsonSerializer.SerializeToElement(schema);

        var meta = tool.Meta ?? new JsonObject();
        meta["securitySchemes"] = new JsonArray(new JsonObject
        {
            ["type"] = "oauth2",
            ["scopes"] = new JsonArray(tool.Annotations?.ReadOnlyHint == true ? ReadScope : WriteScope),
        });
        if (tool.Name == ProfileToolName)
        {
            meta["openai/profile"] = true;
        }

        tool.Meta = meta;
        return tool;
    }
}
