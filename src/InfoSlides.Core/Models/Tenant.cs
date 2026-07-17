namespace InfoSlides.Core.Models;

public sealed record CreateTenantRequest(string TenantName, string OwnerEmail);

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
