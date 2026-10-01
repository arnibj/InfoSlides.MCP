// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace InfoSlides.McpServer;

/// <summary>
/// Lets an <c>isk_</c> API key reach the tools. The host cannot validate a key (only the API holds them), so it
/// accepts the shape and forwards the header untouched; the API then authenticates it, enforces its role and scope,
/// and answers with its normal error when the key is wrong or revoked. This keeps keys working exactly as they do
/// against <c>/v1</c> without the host storing or inspecting any credential.
/// </summary>
public sealed class ApiKeyPassThroughHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The authentication scheme name.</summary>
    public const string SchemeName = "ApiKeyPassThrough";

    /// <summary>The prefix every API key starts with.</summary>
    public const string KeyPrefix = "isk_";

    /// <summary>
    /// Returns the bearer credential from an <c>Authorization</c> header, or null when it is not a Bearer header.
    /// </summary>
    /// <param name="authorizationHeader">The raw header value.</param>
    /// <returns>The credential text.</returns>
    public static string? ExtractBearer(string? authorizationHeader)
    {
        const string prefix = "Bearer ";
        return authorizationHeader is not null && authorizationHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeader[prefix.Length..].Trim()
            : null;
    }

    /// <summary>Whether the header carries an API key.</summary>
    /// <param name="authorizationHeader">The raw header value.</param>
    /// <returns>True for <c>Bearer isk_...</c>.</returns>
    public static bool IsApiKey(string? authorizationHeader) =>
        ExtractBearer(authorizationHeader) is { } token && token.StartsWith(KeyPrefix, StringComparison.Ordinal) && token.Length >= 16;

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!IsApiKey(Request.Headers.Authorization.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim("auth_method", "api_key")], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
