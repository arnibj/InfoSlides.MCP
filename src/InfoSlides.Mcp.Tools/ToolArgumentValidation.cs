// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InfoSlides.Mcp.Tools;

/// <summary>
/// Checks a tool call's arguments against the tool's input schema before the tool runs. Without it a missing
/// required argument surfaced only as the SDK's "An error occurred invoking", and an argument the tool does not take
/// was silently dropped. Both now answer <c>ValidationFailed</c> naming the argument, as the REST API names an
/// unknown body field.
/// </summary>
public static class ToolArgumentValidation
{
    /// <summary>
    /// The call-tool filter: refuses the call with <c>ValidationFailed</c> when <see cref="Problem"/> finds one.
    /// </summary>
    /// <param name="next">The rest of the pipeline.</param>
    /// <returns>The filtering handler.</returns>
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Filter(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) => async (context, ct) =>
    {
        if (context.MatchedPrimitive is McpServerTool tool &&
            Problem(tool.ProtocolTool.InputSchema, context.Params?.Arguments) is { } problem)
        {
            return ToolResults.ValidationError(problem);
        }

        return await next(context, ct);
    };

    /// <summary>
    /// Finds what is wrong with the arguments: required ones missing (or sent as null), then ones the schema does
    /// not list.
    /// </summary>
    /// <param name="inputSchema">The tool's input schema (a JSON Schema object).</param>
    /// <param name="arguments">The call's arguments, if any.</param>
    /// <returns>A message naming the arguments to fix, or null when they are fine.</returns>
    public static string? Problem(JsonElement inputSchema, IDictionary<string, JsonElement>? arguments)
    {
        var properties = inputSchema.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object
            ? p.EnumerateObject().ToList()
            : [];

        var missing = (inputSchema.TryGetProperty("required", out var r) && r.ValueKind == JsonValueKind.Array
                ? r.EnumerateArray().Select(e => e.GetString()).OfType<string>()
                : [])
            .Where(name => arguments is null || !arguments.TryGetValue(name, out var value) || value.ValueKind == JsonValueKind.Null)
            .ToList();

        if (missing.Count > 0)
        {
            return "Missing required " + string.Join("; ", missing.Select(name =>
            {
                var described = properties.FirstOrDefault(x => x.Name == name).Value;
                return described.ValueKind == JsonValueKind.Object && described.TryGetProperty("description", out var d)
                    ? $"argument '{name}' ({d.GetString()?.TrimEnd('.')})"
                    : $"argument '{name}'";
            })) + ".";
        }

        var unknown = (arguments?.Keys ?? []).Where(name => properties.All(x => x.Name != name)).ToList();
        if (unknown.Count > 0)
        {
            return $"This tool does not take {string.Join(", ", unknown.Select(n => $"'{n}'"))}. " +
                   $"It takes: {string.Join(", ", properties.Select(x => x.Name))}.";
        }

        return null;
    }
}
