// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static InfoSlides.McpServer.Tests.McpRpc;

namespace InfoSlides.McpServer.Tests;

/// <summary>
/// Browser-based MCP clients (MCP Inspector) discover and call the host from their own origin: the preflight on
/// <c>/mcp</c> must pass, the 401 challenge must be readable, and the resource metadata must carry the allow-origin
/// header. Any origin, never credentials (the host authenticates by bearer header only).
/// </summary>
public sealed class McpHostCorsTests(McpHostFactory factory) : IClassFixture<McpHostFactory>
{
    private const string Origin = "http://localhost:6274";

    [Fact]
    public async Task Preflight_OnMcp_AllowsAnyOrigin_AndTheMcpHeaders_WithoutCredentials()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/mcp");
        request.Headers.Add("Origin", Origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,mcp-protocol-version");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        var allowed = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant();
        Assert.Contains("authorization", allowed);
        Assert.Contains("mcp-protocol-version", allowed);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task UnauthenticatedCall_FromABrowser_CanReadTheChallenge()
    {
        var client = factory.CreateClient();
        using var request = Rpc(Initialize());
        request.Headers.Add("Origin", Origin);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("WWW-Authenticate", string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProtectedResourceMetadata_CarriesTheAllowOriginHeader()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://mcp.test") });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/oauth-protected-resource/mcp");
        request.Headers.Add("Origin", Origin);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }
}
