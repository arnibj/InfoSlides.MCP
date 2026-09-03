namespace InfoSlides.Core.Models;

public sealed record Device(string Id, string Name, Resolution Resolution);

public sealed record CreateDeviceRequest(string Name, Resolution? Resolution = null);

public sealed record NowPlaying(string? SlideshowId, string? SlideId);

public sealed record DeviceStatus(bool Online, DateTimeOffset? LastSeenAt, NowPlaying? NowPlaying);

public sealed record AssignScheduleRequest(IReadOnlyList<string> SlideshowIds);

public sealed record Schedule(string DeviceId, IReadOnlyList<string> SlideshowIds);

/// <summary>
/// <paramref name="HlsUrl"/> is the raw HLS manifest link and is <c>null</c> whenever the resolved
/// <paramref name="PlaybackMode"/> is <c>"Html"</c> — an HTML-mode slideshow has no HLS stream to
/// link to. <paramref name="PlayerUrl"/> is always populated and plays either mode; it is the URL to
/// hand over when the caller does not already know (or care) which mode the device is in.
/// </summary>
public sealed record StreamLink(string? HlsUrl, DateTimeOffset? ExpiresAt, string PlayerUrl, string PlaybackMode);
