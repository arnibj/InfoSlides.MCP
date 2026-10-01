// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Security.Claims;
using InfoSlides.Mcp.Tools;

namespace InfoSlides.McpServer;

/// <summary>What the host knows about the caller of the current request, read from the authenticated principal.</summary>
public static class CallerContext
{
    /// <summary>The claim the API-key pass-through handler sets.</summary>
    public const string AuthMethodClaim = "auth_method";

    /// <summary>True when the caller authenticated with an <c>isk_</c> API key.</summary>
    /// <param name="user">The authenticated principal.</param>
    /// <returns>Whether the caller is an API key.</returns>
    public static bool IsApiKey(ClaimsPrincipal? user) => user?.FindFirst(AuthMethodClaim)?.Value == "api_key";

    /// <summary>The client surface (<c>chatgpt</c>, <c>claude</c>, <c>gemini</c>, ...) from the access token, if any.</summary>
    /// <param name="user">The authenticated principal.</param>
    /// <returns>The surface claim value.</returns>
    public static string? Surface(ClaimsPrincipal? user) => user?.FindFirst("surface")?.Value;

    /// <summary>The tool profile this caller is offered.</summary>
    /// <param name="user">The authenticated principal.</param>
    /// <returns>The profile.</returns>
    public static ToolProfile ProfileFor(ClaimsPrincipal? user) => HostedToolProfile.ProfileFor(IsApiKey(user), Surface(user));
}
