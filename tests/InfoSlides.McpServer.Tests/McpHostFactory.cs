// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace InfoSlides.McpServer.Tests;

/// <summary>
/// Hosts the real MCP server in memory with a fake InfoSlides API behind its tools, and a test key standing in for
/// the authorization server's JWKS, so tests exercise the genuine HTTP pipeline: authentication, the MCP transport,
/// tool execution and the token forwarded to the API.
/// </summary>
public class McpHostFactory : WebApplicationFactory<Program>
{
    /// <summary>Issuer the test host trusts.</summary>
    public const string Issuer = "https://auth.test";

    /// <summary>The MCP resource (audience) of the test host.</summary>
    public const string Resource = "https://mcp.test/mcp";

    private readonly RSA _rsa = RSA.Create(2048);

    /// <summary>Every request the tools made to the fake API, in order.</summary>
    public List<RecordedApiRequest> ApiRequests { get; } = [];

    /// <summary>Mints an access token the host should accept, with overridable claims for negative tests.</summary>
    /// <param name="audience">The token audience.</param>
    /// <param name="issuer">The token issuer.</param>
    /// <param name="expires">Expiry; defaults to one hour from now.</param>
    /// <param name="signingKey">An alternative signing key; defaults to the trusted one.</param>
    /// <returns>The compact JWT.</returns>
    public string MintToken(string audience = Resource, string issuer = Issuer, DateTime? expires = null, SecurityKey? signingKey = null, string? surface = null, string? name = null, string? subject = null)
    {
        var key = signingKey ?? new RsaSecurityKey(_rsa) { KeyId = "k1" };
        var credentials = new SigningCredentials(key, key is RsaSecurityKey ? SecurityAlgorithms.RsaSha256 : SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            Claims(surface, name, subject),
            DateTime.UtcNow.AddHours(-2),
            expires ?? DateTime.UtcNow.AddHours(1),
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static List<Claim> Claims(string? surface, string? name, string? subject)
    {
        var claims = new List<Claim>
        {
            new("sub", subject ?? Guid.NewGuid().ToString()),
            new("tenant_id", Guid.NewGuid().ToString()),
            new("client_id", surface ?? "custom-client"),
            new("scope", "infoslides.read infoslides.write"),
        };
        if (surface is not null)
        {
            claims.Add(new Claim("surface", surface));
        }

        if (name is not null)
        {
            claims.Add(new Claim("name", name));
        }

        return claims;
    }

    /// <summary>Extra configuration a test class can set before the host starts (for example a tiny rate limit).</summary>
    public Dictionary<string, string> Settings { get; } = [];

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("McpHost:Issuer", Issuer);
        builder.UseSetting("McpHost:ResourceIdentifier", Resource);
        builder.UseSetting("McpHost:ApiUrl", "https://api.test");
        foreach (var (key, value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            // Stand in for the authorization server's published keys (normally fetched from its OpenID discovery).
            services.PostConfigureAll<JwtBearerOptions>(options =>
            {
                options.Authority = null;
                options.MetadataAddress = null!;
                options.RequireHttpsMetadata = false;
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                configuration.SigningKeys.Add(new RsaSecurityKey(_rsa) { KeyId = "k1" });
                options.Configuration = configuration;
                options.ConfigurationManager = new Microsoft.IdentityModel.Protocols.StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });

            services.AddHttpClient("infoslides-api")
                .ConfigurePrimaryHttpMessageHandler(() => new FakeApiHandler(ApiRequests));
        });
    }

    /// <summary>One call a tool made to the fake API.</summary>
    /// <param name="Method">HTTP method.</param>
    /// <param name="Path">Request path and query.</param>
    /// <param name="Authorization">The Authorization header the host forwarded.</param>
    public sealed record RecordedApiRequest(string Method, string Path, string? Authorization);

    private sealed class FakeApiHandler(List<RecordedApiRequest> requests) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var auth = request.Headers.Authorization?.ToString();
            lock (requests)
            {
                requests.Add(new RecordedApiRequest(request.Method.Method, request.RequestUri!.PathAndQuery, auth));
            }

            if (auth is not null && auth.Contains("isk_revoked", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("""{"error":{"code":"Unauthorized","message":"Invalid API key."}}""", Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[]}""", Encoding.UTF8, "application/json"),
            });
        }
    }
}
