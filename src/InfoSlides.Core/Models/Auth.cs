namespace InfoSlides.Core.Models;

public sealed record CliCodeExchangeRequest(string Code, string CodeVerifier);

public sealed record SessionInfo(
    string SessionToken,
    DateTimeOffset ExpiresAt,
    string TenantId,
    string Email);
