using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using InfoSlides.Core.Api;
using InfoSlides.Core.Serialization;
using ModelContextProtocol.Protocol;

namespace InfoSlides.Cli.Tools;

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

    public static CallToolResult ValidationError(string message) =>
        ErrorResult("ValidationFailed", message, null);

    private static bool IsToolError(Exception exception) =>
        exception is ApiException or MissingCredentialException or HttpRequestException;

    private static CallToolResult Error(Exception exception) => exception switch
    {
        ApiException api => ErrorResult(api.Code, api.Message, api.UpgradeUrl),
        MissingCredentialException missing => ErrorResult("MissingCredential", missing.Message, null),
        _ => ErrorResult("NetworkError", $"Could not reach the InfoSlides API: {exception.Message}", null),
    };

    private static CallToolResult ErrorResult(string code, string message, string? upgradeUrl)
    {
        var error = new JsonObject { ["code"] = code, ["message"] = message };
        if (upgradeUrl is not null)
        {
            error["upgradeUrl"] = upgradeUrl;
        }

        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = new JsonObject { ["error"] = error }.ToJsonString() }],
        };
    }
}
