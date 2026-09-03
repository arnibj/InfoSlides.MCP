using System.Text.Json;

namespace InfoSlides.Core.Models;

public sealed record SlideCondition(string Type, string Value);

public sealed record Slide(
    string Id,
    string? MediaUrl = null,
    string? TemplateId = null,
    double? DurationSeconds = null,
    int? Position = null,
    IReadOnlyList<SlideCondition>? Conditions = null);

/// <summary>
/// <paramref name="PlaybackModeOverride"/> is this slideshow's own playback-mode override
/// (<c>"VideoStream"</c> or <c>"Html"</c>), or null when it inherits the tenant/system default.
/// <paramref name="EffectivePlaybackMode"/> is always populated — the resolved mode actually used
/// when the slideshow streams (override, else tenant default, else system default; defaults to
/// <c>"VideoStream"</c> when nothing overrides anything).
/// </summary>
public sealed record Slideshow(
    string Id,
    string Title,
    Resolution Resolution,
    IReadOnlyList<Slide>? Slides = null,
    string? PlaybackModeOverride = null,
    string EffectivePlaybackMode = "VideoStream");

public sealed record CreateSlideshowRequest(
    string Title,
    Resolution? Resolution = null,
    IReadOnlyList<NewSlide>? Slides = null);

public sealed record NewSlide(
    string? MediaUrl = null,
    string? TemplateId = null,
    double? DurationSeconds = null);

/// <summary>
/// <paramref name="PlaybackMode"/> is three-state on the wire: omit it (leave <c>null</c> here) to
/// leave the slideshow's playback-mode override alone; pass the literal string <c>"inherit"</c> to
/// clear the override back to the tenant/system default; pass <c>"VideoStream"</c> or <c>"Html"</c>
/// to set it. The enum names are matched case-sensitively by the backend; <c>"inherit"</c> is not.
/// </summary>
public sealed record UpdateSlideshowRequest(
    string? Title = null,
    Resolution? Resolution = null,
    IReadOnlyList<string>? SlideOrder = null,
    string? PlaybackMode = null);

/// <summary>
/// Request body for <c>POST /v1/slideshows/{id}/slides</c>. Exactly one of
/// <see cref="MediaUrl"/> (downloaded server-side) / <see cref="MediaAssetId"/> (an id already in
/// the tenant's media library, e.g. from <c>POST /v1/media</c>) must be set.
/// </summary>
public sealed record AddMediaSlideRequest(
    string? MediaUrl = null,
    string? MediaAssetId = null,
    double? DurationSeconds = null,
    int? Position = null);

/// <summary>Result of <c>POST /v1/media</c> — pass <see cref="Id"/> as <c>mediaAssetId</c> to <see cref="AddMediaSlideRequest"/>.</summary>
public sealed record UploadedMedia(string Id, string FileType, int? Width = null, int? Height = null);

/// <summary>
/// Request body for <c>POST /v1/slideshows/{id}/slides/dynamic</c>. The new slide starts with no
/// content source and empty override data — push initial/ongoing data with <c>source update</c>.
/// </summary>
public sealed record AddDynamicSlideRequest(
    string TemplateId,
    double? DurationSeconds = null,
    int? Position = null);

public sealed record SetConditionsRequest(IReadOnlyList<SlideCondition> Conditions);

public sealed record Template(
    string Id,
    string Title,
    JsonElement? SampleJson = null,
    string? Html = null,
    string? Css = null);

public sealed record CreateTemplateRequest(
    string Title,
    string? Prompt = null,
    JsonElement? SampleJson = null,
    string? Html = null,
    string? Css = null);

public sealed record GalleryItem(
    string Id,
    string Title,
    string? Description,
    string? PreviewUrl,
    Resolution? Resolution);
