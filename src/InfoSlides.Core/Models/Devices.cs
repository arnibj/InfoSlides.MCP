namespace InfoSlides.Core.Models;

public sealed record Device(string Id, string Name, Resolution Resolution);

public sealed record CreateDeviceRequest(string Name, Resolution? Resolution = null);

public sealed record NowPlaying(string? SlideshowId, string? SlideId);

public sealed record DeviceStatus(bool Online, DateTimeOffset? LastSeenAt, NowPlaying? NowPlaying);

public sealed record AssignScheduleRequest(IReadOnlyList<string> SlideshowIds);

public sealed record Schedule(string DeviceId, IReadOnlyList<string> SlideshowIds);

public sealed record StreamLink(string HlsUrl, DateTimeOffset? ExpiresAt);
