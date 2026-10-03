// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Xunit.Abstractions;
using static InfoSlides.McpServer.Tests.McpRpc;

namespace InfoSlides.McpServer.Tests;

/// <summary>
/// OAI-05: proves the MCP SDK's HTTP transport, hosted next to the shared tool classes, behaves
/// correctly over real HTTP: 401 with <c>resource_metadata</c>, token validation, protocol negotiation,
/// 202 for notifications, token forwarding to the API, API keys, and what the SDK does and does not emit.
/// </summary>
public sealed class McpHostTransportTests(McpHostFactory factory, ITestOutputHelper output) : IClassFixture<McpHostFactory>
{
    [Fact]
    public async Task NoToken_Is401WithResourceMetadataChallenge()
    {
        var client = factory.CreateClient();

        var response = await client.SendAsync(Rpc(Initialize()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = string.Join(" ", response.Headers.WwwAuthenticate.Select(h => h.ToString()));
        output.WriteLine("WWW-Authenticate: " + challenge);
        Assert.Contains("resource_metadata=\"https://mcp.test/.well-known/oauth-protected-resource/mcp\"", challenge);
    }

    [Fact]
    public async Task ProtectedResourceMetadata_NamesTheResourceAndIssuer()
    {
        // The document is served on the host named in the configured resource, as it is behind the real domain.
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://mcp.test") });

        var response = await client.GetAsync("/.well-known/oauth-protected-resource/mcp");

        output.WriteLine($"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(McpHostFactory.Resource, doc.GetProperty("resource").GetString());
        Assert.Contains(doc.GetProperty("authorization_servers").EnumerateArray(), e => e.GetString() == McpHostFactory.Issuer);
    }

    [Theory]
    [InlineData("2025-11-25")]
    [InlineData("2025-06-18")]
    [InlineData("2025-03-26")]
    public async Task Initialize_NegotiatesTheProtocolVersion(string requested)
    {
        var client = factory.CreateClient();

        var response = await client.SendAsync(Rpc(Initialize(requested), factory.MintToken()));
        var body = await ReadRpcAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var negotiated = body.GetProperty("result").GetProperty("protocolVersion").GetString();
        output.WriteLine($"requested {requested} -> {negotiated}");
        Assert.Equal(requested, negotiated);
    }

    [Fact]
    public async Task InitializedNotification_Is202WithoutBody()
    {
        var client = factory.CreateClient();

        var response = await client.SendAsync(Rpc(new { jsonrpc = "2.0", method = "notifications/initialized" }, factory.MintToken()));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task ToolsList_ExposesTheSharedTools_WithAnnotations()
    {
        var client = factory.CreateClient();

        var response = await client.SendAsync(Rpc(new { jsonrpc = "2.0", id = 2, method = "tools/list" }, factory.MintToken(surface: "gemini")));
        var body = await ReadRpcAsync(response);

        var result = body.GetProperty("result");
        var tools = result.GetProperty("tools").EnumerateArray().ToList();
        output.WriteLine("tools/list result keys: " + string.Join(", ", result.EnumerateObject().Select(p => p.Name)));
        output.WriteLine("first tool: " + tools[0].GetRawText());
        Assert.Equal(70, tools.Count); // 72 shared tools - 3 that need a local file + the host's profile tool
        Assert.All(tools, t =>
        {
            var annotations = t.GetProperty("annotations");
            Assert.True(annotations.TryGetProperty("readOnlyHint", out _), t.GetProperty("name").GetString());
            Assert.True(annotations.TryGetProperty("destructiveHint", out _));
            Assert.True(annotations.TryGetProperty("openWorldHint", out _));
            Assert.True(annotations.TryGetProperty("title", out _));
        });
        output.WriteLine("ttlMs: " + (result.TryGetProperty("ttlMs", out var ttl) ? ttl.GetRawText() : "absent") + ", cacheScope: " + (result.TryGetProperty("cacheScope", out var scope) ? scope.GetRawText() : "absent"));
        Assert.Equal(3600000, ttl.GetInt64());
        Assert.Equal("private", scope.GetString());
    }

    [Fact]
    public async Task ToolCall_ForwardsTheCallersOwnTokenToTheApi()
    {
        var client = factory.CreateClient();
        var token = factory.MintToken();
        factory.ApiRequests.Clear();

        var response = await client.SendAsync(Rpc(new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new { name = "list_slideshows", arguments = new { } },
        }, token));
        var body = await ReadRpcAsync(response);

        output.WriteLine(body.GetRawText());
        Assert.False(body.GetProperty("result").TryGetProperty("isError", out var isError) && isError.GetBoolean());
        var call = Assert.Single(factory.ApiRequests);
        Assert.Equal("GET", call.Method);
        Assert.StartsWith("/v1/slideshows", call.Path);
        Assert.Equal("Bearer " + token, call.Authorization);
    }

    [Fact]
    public async Task ToolCalls_InParallel_EachForwardItsOwnCredential()
    {
        var client = factory.CreateClient();
        factory.ApiRequests.Clear();
        var tokens = Enumerable.Range(0, 8).Select(_ => factory.MintToken()).ToList();

        await Task.WhenAll(tokens.Select(async t =>
        {
            var response = await client.SendAsync(Rpc(new { jsonrpc = "2.0", id = 4, method = "tools/call", @params = new { name = "list_slideshows", arguments = new { } } }, t));
            await ReadRpcAsync(response);
        }));

        var forwarded = factory.ApiRequests.Select(r => r.Authorization ?? string.Empty).OrderBy(a => a).ToList();
        Assert.Equal(tokens.Select(t => "Bearer " + t).OrderBy(a => a).ToList(), forwarded);
    }

    [Fact]
    public async Task ApiKey_IsAcceptedAndForwardedUnchanged()
    {
        var client = factory.CreateClient();
        const string key = "isk_abcdefghijklmnopqrstuvwxyz012345";
        factory.ApiRequests.Clear();

        var list = await ReadRpcAsync(await client.SendAsync(Rpc(new { jsonrpc = "2.0", id = 2, method = "tools/list" }, key)));
        var call = await ReadRpcAsync(await client.SendAsync(Rpc(new { jsonrpc = "2.0", id = 3, method = "tools/call", @params = new { name = "list_slideshows", arguments = new { } } }, key)));

        Assert.Equal(69, list.GetProperty("result").GetProperty("tools").GetArrayLength()); // 72 shared - 3 local-file tools
        output.WriteLine(call.GetRawText());
        Assert.Equal("Bearer " + key, Assert.Single(factory.ApiRequests).Authorization);
    }

    [Fact]
    public async Task RevokedApiKey_IsRefusedByTheApiAndReportedAsAToolError()
    {
        var client = factory.CreateClient();

        var call = await ReadRpcAsync(await client.SendAsync(Rpc(new
        {
            jsonrpc = "2.0",
            id = 3,
            method = "tools/call",
            @params = new { name = "list_slideshows", arguments = new { } },
        }, "isk_revoked_000000000000000000")));

        output.WriteLine(call.GetRawText());
        Assert.True(call.GetProperty("result").GetProperty("isError").GetBoolean());
    }

    public static IEnumerable<object[]> BadTokens()
    {
        yield return ["wrong-audience"];
        yield return ["wrong-issuer"];
        yield return ["expired"];
        yield return ["other-rsa-key"];
        yield return ["hs256"];
        yield return ["garbage"];
    }

    [Theory]
    [MemberData(nameof(BadTokens))]
    public async Task InvalidTokens_AreRefusedWith401(string kind)
    {
        var client = factory.CreateClient();
        using var otherRsa = System.Security.Cryptography.RSA.Create(2048);
        var token = kind switch
        {
            "wrong-audience" => factory.MintToken(audience: "https://infoslides.app/v1"),
            "wrong-issuer" => factory.MintToken(issuer: "https://evil.test"),
            "expired" => factory.MintToken(expires: DateTime.UtcNow.AddMinutes(-10)),
            "other-rsa-key" => factory.MintToken(signingKey: new RsaSecurityKey(otherRsa) { KeyId = "k1" }),
            "hs256" => factory.MintToken(signingKey: new SymmetricSecurityKey(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray())),
            _ => "not.a.jwt",
        };

        var response = await client.SendAsync(Rpc(Initialize(), token));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMcp_IsEitherAnEventStreamOr405()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", factory.MintToken());

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        output.WriteLine($"GET /mcp -> {(int)response.StatusCode} {response.Content.Headers.ContentType}");
        Assert.True(response.StatusCode == HttpStatusCode.MethodNotAllowed || response.Content.Headers.ContentType?.MediaType == "text/event-stream");
    }

    [Fact]
    public async Task Healthz_IsAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
