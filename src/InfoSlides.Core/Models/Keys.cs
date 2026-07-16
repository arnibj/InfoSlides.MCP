namespace InfoSlides.Core.Models;

public sealed record CreateApiKeyRequest(
    string Type,
    string Name,
    IReadOnlyList<string>? SlideIds = null);

public sealed record CreateApiKeyResult(string Id, string Key, string Type, string Name);

public sealed record ApiKeyInfo(
    string Id,
    string Name,
    string Type,
    string Prefix,
    IReadOnlyList<string>? SlideIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);

public sealed record CheckoutLink(string CheckoutUrl);
