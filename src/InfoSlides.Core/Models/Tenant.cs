namespace InfoSlides.Core.Models;

/// <summary>
/// Body of <c>POST /v1/tenants</c>.
/// </summary>
/// <param name="TenantName">Display name for the new workspace.</param>
/// <param name="OwnerEmail">The owner's email address.</param>
/// <param name="Source">How the signup originated — <c>"mcp"</c> from the MCP server, <c>"cli"</c>
/// from the command line. Optional on the wire; a backend that predates signup attribution ignores
/// it, and one that supports it records <c>Unknown</c> when it is omitted.</param>
public sealed record CreateTenantRequest(string TenantName, string OwnerEmail, string? Source = null);

public sealed record CreateTenantResult(string TenantId, string ApiKey, bool VerificationEmailSent);

public sealed record DeviceQuota(int Used, int Max);

public sealed record KeyScope(string Type, IReadOnlyList<string>? SlideIds);

public sealed record TenantInfo(
    string TenantId,
    string Name,
    string OwnerEmail,
    bool IsEmailVerified,
    string SubscriptionLevel,
    DeviceQuota DeviceQuota,
    KeyScope? KeyScope);
