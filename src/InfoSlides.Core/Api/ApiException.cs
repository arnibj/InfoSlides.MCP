using System.Net;
using System.Text.Json;

namespace InfoSlides.Core.Api;

/// <summary>A structured error returned by the InfoSlides API (contract §2).</summary>
public sealed class ApiException : Exception
{
    public ApiException(string code, string message, HttpStatusCode statusCode, JsonElement? details = null)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Details = details;
    }

    public string Code { get; }
    public HttpStatusCode StatusCode { get; }
    public JsonElement? Details { get; }

    /// <summary>Paddle checkout link supplied with EntitlementRequired errors, when present.</summary>
    public string? UpgradeUrl =>
        Details is { ValueKind: JsonValueKind.Object } d &&
        d.TryGetProperty("upgradeUrl", out var url) && url.ValueKind == JsonValueKind.String
            ? url.GetString()
            : null;
}

/// <summary>
/// Thrown client-side when a call requiring credentials is attempted without any configured,
/// so agents get actionable guidance instead of a bare 401.
/// </summary>
public sealed class MissingCredentialException() : InvalidOperationException(
    "No InfoSlides credentials configured. Run `infoslides login`, set the INFOSLIDES_API_KEY " +
    "environment variable, or pass --api-key. New users can create a workspace with " +
    "`infoslides tenant create` (create_tenant), which returns an API key.");
