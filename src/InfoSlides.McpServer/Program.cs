// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using InfoSlides.Mcp.Tools;
using InfoSlides.Core.Api;
using InfoSlides.McpServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<McpHostOptions>(builder.Configuration.GetSection(McpHostOptions.SectionName));
var host = builder.Configuration.GetSection(McpHostOptions.SectionName).Get<McpHostOptions>() ?? new McpHostOptions();

const string PolicyScheme = "McpBearerOrKey";
const string OAuthScheme = JwtBearerDefaults.AuthenticationScheme;

// Authentication. OAuth access tokens (RS256, issued by the InfoSlides authorization server) are validated here
// against the issuer's published keys; an isk_ API key is passed through for the API to validate. An
// unauthenticated request is challenged by the MCP handler, which answers 401 with
// WWW-Authenticate: Bearer resource_metadata="..." so clients can discover the authorization server.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = PolicyScheme;
        options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    })
    .AddPolicyScheme(PolicyScheme, "OAuth bearer or API key", options =>
    {
        options.ForwardDefaultSelector = context =>
            ApiKeyPassThroughHandler.IsApiKey(context.Request.Headers.Authorization.ToString())
                ? ApiKeyPassThroughHandler.SchemeName
                : OAuthScheme;
    })
    .AddScheme<AuthenticationSchemeOptions, ApiKeyPassThroughHandler>(ApiKeyPassThroughHandler.SchemeName, _ => { })
    .AddJwtBearer(OAuthScheme, options =>
    {
        options.Authority = host.Issuer;
        options.RequireHttpsMetadata = !host.AllowInsecureMetadata;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = host.Issuer,
            ValidateAudience = true,
            ValidAudience = host.ResourceIdentifier,
            ValidateLifetime = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    })
    .AddMcp(options =>
    {
        options.ResourceMetadataUri = host.GetResourceMetadataUri();
        options.ResourceMetadata = new ProtectedResourceMetadata
        {
            Resource = host.ResourceIdentifier,
            AuthorizationServers = { host.Issuer },
            ScopesSupported = host.ScopesSupported.ToList(),
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddOutputCache();

// The tools call the InfoSlides API with the caller's own credential (OAuth token or API key), forwarded per
// request; the host stores nothing.
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("infoslides-api");
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<IOptions<McpHostOptions>>().Value;
    var credential = ApiKeyPassThroughHandler.ExtractBearer(
        sp.GetRequiredService<IHttpContextAccessor>().HttpContext?.Request.Headers.Authorization.ToString());
    return new InfoSlidesApiClient(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("infoslides-api"),
        new Uri(options.ApiUrl),
        credential);
});

// The host's assembly version (set from the CLI project's <Version>, so one number covers both).
var serverVersion = typeof(Program).Assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "infoslides", Title = "InfoSlides", Version = serverVersion };
    })
    .WithHttpTransport(options =>
    {
        options.Stateless = true;

        // Hosts that cannot load the skill (the Gemini app) get their only guidance here, per caller profile.
        options.ConfigureSessionOptions = (httpContext, serverOptions, _) =>
        {
            serverOptions.ServerInstructions = ToolProfileFilters.InstructionsFor(httpContext.User);
            return Task.CompletedTask;
        };
    })
    .WithTools<ProfileTools>()
    .WithInfoSlidesTools(InfoSlidesToolRegistration.CreateJsonOptions())
    .WithProfileFilters();

// Limits are per caller, never per IP alone: hosted assistants call from shared cloud address ranges.
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(CallerPartition(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = host.RequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

// Browser-based clients (MCP Inspector) fetch the resource metadata and call /mcp from their own origin. Any
// origin, never credentials: the host authenticates by bearer header only, so an open policy grants a page nothing it
// could not do without CORS. The client must read WWW-Authenticate (the 401 challenge) and Mcp-Session-Id.
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .AllowAnyOrigin()
    .WithMethods("GET", "POST", "DELETE")
    .WithHeaders("Authorization", "Content-Type", "Accept", "Mcp-Protocol-Version", "Mcp-Session-Id", "Last-Event-ID")
    .WithExposedHeaders("WWW-Authenticate", "Mcp-Session-Id")));

var app = builder.Build();

// Behind nginx on the same machine: trust its forwarded address and scheme (loopback only by default).
app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });
// Before authentication: the resource metadata document and the 401 challenge come from the authentication handler.
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseOutputCache();

app.MapMcp("/mcp").RequireAuthorization();
// Static server card for directories that cannot sign in to scan (Smithery). Built from the tools the host registers,
// in the hosted profile, so it cannot drift from tools/list. Short cache: Cloudflare must not hold a stale card long.
app.MapGet("/.well-known/mcp/server-card.json", (IEnumerable<McpServerTool> tools) =>
{
    var listed = ToolProfileFilters.ProfileTools(tools.Select(t => t.ProtocolTool), ToolProfile.Hosted, surface: null, isApiKey: false);
    return Results.Json(new
    {
        serverInfo = new { name = "infoslides", version = serverVersion },
        authentication = new { required = true, schemes = new[] { "oauth2" } },
        tools = listed.Select(t => new { name = t.Name, description = t.Description, inputSchema = t.InputSchema }),
        resources = Array.Empty<object>(),
        prompts = Array.Empty<object>(),
    }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
}).AllowAnonymous().CacheOutput(p => p.Expire(TimeSpan.FromMinutes(5)));
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

static string CallerPartition(HttpContext context)
{
    var user = context.User;
    var client = user.FindFirst("client_id")?.Value;
    var subject = user.FindFirst("sub")?.Value;
    if (client is not null && subject is not null)
    {
        return $"oauth:{client}:{subject}";
    }

    var bearer = ApiKeyPassThroughHandler.ExtractBearer(context.Request.Headers.Authorization.ToString());
    if (bearer is not null && ApiKeyPassThroughHandler.IsApiKey(context.Request.Headers.Authorization.ToString()))
    {
        return "key:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(bearer)))[..16];
    }

    return "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}

/// <summary>Entry point marker so integration tests can host the application.</summary>
public partial class Program;
