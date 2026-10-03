// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

namespace InfoSlides.McpServer;

/// <summary>
/// Configuration of the hosted MCP server (section <c>McpHost</c>). Nothing secret lives here: the host stores no
/// credentials and validates OAuth access tokens against the authorization server's published keys.
/// </summary>
public sealed class McpHostOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "McpHost";

    /// <summary>Base URL of the InfoSlides API the tools call, with the caller's own token forwarded.</summary>
    public string ApiUrl { get; set; } = "https://infoslides.app";

    /// <summary>The authorization server issuer; its JWKS validates access tokens (via OpenID discovery).</summary>
    public string Issuer { get; set; } = "https://infoslides.app";

    /// <summary>
    /// The MCP resource identifier. It is the URL clients connect to, the protected resource metadata
    /// <c>resource</c>, and the audience an access token must carry.
    /// </summary>
    public string ResourceIdentifier { get; set; } = "https://infoslides.app/mcp";

    /// <summary>Scopes advertised in the protected resource metadata.</summary>
    public string[] ScopesSupported { get; set; } = ["infoslides.read", "infoslides.write", "offline_access"];

    /// <summary>
    /// The URL of the protected resource metadata document that 401 challenges point to. Defaults to the standard
    /// location for <see cref="ResourceIdentifier"/> (RFC 9728): the origin, <c>/.well-known/oauth-protected-resource</c>,
    /// then the resource path. Set it explicitly only when a proxy serves it elsewhere.
    /// </summary>
    public string? ResourceMetadataUri { get; set; }

    /// <summary>Requests allowed per minute for one caller (token client and subject, API key, or address).</summary>
    public int RequestsPerMinute { get; set; } = 240;

    /// <summary>Whether discovery documents may be fetched over plain http (development only).</summary>
    public bool AllowInsecureMetadata { get; set; }

    /// <summary>The metadata URL to challenge with: configured, else derived from the resource identifier.</summary>
    /// <returns>An absolute URI.</returns>
    public Uri GetResourceMetadataUri()
    {
        if (!string.IsNullOrWhiteSpace(ResourceMetadataUri))
        {
            return new Uri(ResourceMetadataUri);
        }

        var resource = new Uri(ResourceIdentifier);
        return new Uri(resource, "/.well-known/oauth-protected-resource" + resource.AbsolutePath.TrimEnd('/'));
    }
}
