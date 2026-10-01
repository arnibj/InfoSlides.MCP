using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using InfoSlides.Core.Api;
using InfoSlides.Core.Serialization;
using ModelContextProtocol.Protocol;

namespace InfoSlides.Mcp.Tools;

/// <summary>
/// Shared plumbing for MCP tools: serializes API results (including warnings such as
/// AspectMismatch) into the tool's text content, and maps known failures to in-band tool errors
/// the calling agent can act on.
/// </summary>
internal static class ToolResults
{
    public static async Task<CallToolResult> Execute<T>(Func<Task<ApiResult<T>>> call, JsonTypeInfo<T> typeInfo)
    {
        try
        {
            var result = await call().ConfigureAwait(false);
            var envelope = new JsonObject
            {
                ["data"] = JsonSerializer.SerializeToNode(result.Data, typeInfo),
            };
            if (result.HasWarnings)
            {
                envelope["warnings"] = JsonSerializer.SerializeToNode(
                    result.Warnings.ToList(), InfoSlidesJsonContext.Default.ListApiWarning);
            }

            if (result.Undo is { } undo)
            {
                // A ready-made request that reverts this change; undo_change takes its token.
                envelope["undo"] = JsonNode.Parse(undo.GetRawText());
            }

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = envelope.ToJsonString() }],
            };
        }
        catch (Exception exception) when (IsToolError(exception))
        {
            return Error(exception);
        }
    }

    public static async Task<CallToolResult> ExecutePng(Func<Task<byte[]>> call, string label)
    {
        try
        {
            var png = await call().ConfigureAwait(false);
            return new CallToolResult
            {
                Content =
                [
                    // ImageContentBlock.Data holds the base64 text itself as UTF-8 bytes.
                    new ImageContentBlock
                    {
                        Data = System.Text.Encoding.UTF8.GetBytes(Convert.ToBase64String(png)),
                        MimeType = "image/png",
                    },
                    new TextContentBlock { Text = label },
                ],
            };
        }
        catch (Exception exception) when (IsToolError(exception))
        {
            return Error(exception);
        }
    }

    /// <summary>Builds a JSON request body from the pairs whose value is not null.</summary>
    /// <param name="props">Property names (camelCase, as on the wire) and values.</param>
    /// <returns>The body; empty when nothing is set.</returns>
    public static JsonObject Body(params (string Name, JsonNode? Value)[] props)
    {
        var body = new JsonObject();
        foreach (var (name, value) in props)
        {
            if (value is not null)
            {
                body[name] = value;
            }
        }

        return body;
    }

    /// <summary>Runs a pass-through call (<see cref="InfoSlidesApiClient.SendJsonAsync"/>) as a tool result.</summary>
    public static Task<CallToolResult> Json(Func<Task<ApiResult<JsonElement>>> call) =>
        Execute(call, InfoSlidesJsonContext.Default.JsonElement);

    public static CallToolResult ValidationError(string message) =>
        ErrorResult("ValidationFailed", message, null);

    private static bool IsToolError(Exception exception) =>
        exception is ApiException or MissingCredentialException or HttpRequestException;

    private static CallToolResult Error(Exception exception) => exception switch
    {
        ApiException api => ErrorResult(api.Code, api.Message, api.UpgradeUrl, api.Details),
        MissingCredentialException missing => ErrorResult("MissingCredential", missing.Message, null),
        _ => ErrorResult("NetworkError", $"Could not reach the InfoSlides API: {exception.Message}", null),
    };

    /// <summary>
    /// Builds the in-band error. <paramref name="details"/> is passed on as the API sent it: a
    /// <c>NeedsClarification</c> carries its question and ready-made choices there, and a
    /// <c>ValidationFailed</c> its per-field messages, which the agent needs to recover.
    /// </summary>
    private static CallToolResult ErrorResult(string code, string message, string? upgradeUrl, JsonElement? details = null)
    {
        var error = new JsonObject { ["code"] = code, ["message"] = message };
        if (upgradeUrl is not null)
        {
            error["upgradeUrl"] = upgradeUrl;
        }

        if (details is { ValueKind: JsonValueKind.Object or JsonValueKind.Array } d)
        {
            error["details"] = JsonNode.Parse(d.GetRawText());
        }

        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = new JsonObject { ["error"] = error }.ToJsonString() }],
        };
    }
}
