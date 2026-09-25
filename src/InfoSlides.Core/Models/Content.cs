using System.Text.Json;

namespace InfoSlides.Core.Models;

public sealed record SlideCondition(string Type, string Value);

/// <summary>One visibility rule covering a slide, as read back by <c>get_slideshow</c>.</summary>
/// <param name="Mode"><c>show</c> or <c>hide</c>: what happens while the conditions hold.</param>
/// <param name="Match"><c>all</c> or <c>any</c> of the conditions.</param>
/// <param name="Conditions">The conditions; null when one has no API grammar (read <paramref name="Summary"/>).</param>
/// <param name="ReadOnly">True for a rule set on the web page: <c>set_slide_conditions</c> only replaces the slide's own rule.</param>
/// <param name="Summary">The rule in plain English.</param>
public sealed record SlideRule(
    string Mode,
    string Match,
    IReadOnlyList<SlideCondition>? Conditions,
    bool ReadOnly,
    string Summary);

/// <summary>A screen that is playing a slideshow right now.</summary>
/// <param name="DeviceId">The device id.</param>
/// <param name="Name">The screen's name, e.g. "Front lobby".</param>
/// <param name="PlayerUrl">Plays what the screen shows, in any browser: the link to give the person.</param>
public sealed record Screen(string DeviceId, string Name, string PlayerUrl);

/// <summary>The news ticker along the bottom of the screen. Omitted fields are left unchanged on update.</summary>
/// <param name="Enabled">Whether the ticker shows. Turning it off also clears its sources.</param>
/// <param name="SourceIds">Content sources whose headlines scroll in the ticker (ids from <c>list_sources</c>).</param>
public sealed record Ticker(bool? Enabled = null, IReadOnlyList<string>? SourceIds = null);

/// <summary>The on-screen clock. In a response, null fields mean the workspace default applies.</summary>
/// <param name="Enabled">Whether the clock shows.</param>
/// <param name="Position"><c>TopLeft</c>, <c>TopRight</c>, <c>BottomLeft</c> or <c>BottomRight</c>.</param>
/// <param name="ShowDate">Whether the date shows under the time.</param>
/// <param name="BackgroundColor">Hex colour, e.g. <c>#000000</c> or <c>#00000080</c>.</param>
/// <param name="TextColor">Hex colour, e.g. <c>#ffffff</c>.</param>
public sealed record Clock(
    bool? Enabled = null,
    string? Position = null,
    bool? ShowDate = null,
    string? BackgroundColor = null,
    string? TextColor = null);

/// <summary>A content source (calendar, RSS feed, weather, push...) the workspace can show or scroll in a ticker.</summary>
/// <param name="Id">Source id.</param>
/// <param name="Name">Display name.</param>
/// <param name="AdapterType">What kind of source it is, e.g. <c>RssFeed</c> or <c>Push</c>.</param>
/// <param name="LastFetchedAt">When data last arrived; null before the first fetch.</param>
public sealed record Source(string Id, string Name, string AdapterType, DateTimeOffset? LastFetchedAt = null);

/// <param name="SourceId">
/// On a slide just added from a push template: the Push source created for it, where its data is
/// sent (<c>POST /v1/sources/{id}/data</c>).
/// </param>
/// <param name="PushKey">
/// On a slide added with <c>createPushKey</c>: a push-only <c>isk_dp_</c> key bound to that source.
/// Returned once and never again.
/// </param>
/// <param name="Type"><c>pptx</c> (a page of the uploaded file), <c>media</c> (image/video) or <c>dynamic</c> (live data).</param>
/// <param name="Hidden">True when the slide is hidden from playback.</param>
/// <param name="ThumbnailUrl">A picture of the slide; <c>preview_slide</c> shows it with the same credential.</param>
/// <param name="TemplateName">For a dynamic slide: the name of its template.</param>
/// <param name="Rules">Every visibility rule covering the slide, including ones set on the web page.</param>
public sealed record Slide(
    string Id,
    string? MediaUrl = null,
    string? TemplateId = null,
    double? DurationSeconds = null,
    int? Position = null,
    IReadOnlyList<SlideCondition>? Conditions = null,
    string? SourceId = null,
    string? PushKey = null,
    string? Type = null,
    bool? Hidden = null,
    string? ThumbnailUrl = null,
    string? TemplateName = null,
    IReadOnlyList<SlideRule>? Rules = null);

/// <summary>
/// <paramref name="PlaybackModeOverride"/> is this slideshow's own playback-mode override
/// (<c>"VideoStream"</c> or <c>"Html"</c>), or null when it inherits the tenant/system default.
/// <paramref name="EffectivePlaybackMode"/> is always populated — the resolved mode actually used
/// when the slideshow streams (override, else tenant default, else system default; defaults to
/// <c>"VideoStream"</c> when nothing overrides anything).
/// <paramref name="RenderStatus"/> is <c>Pending</c>, <c>Rendering</c>, <c>Completed</c> or <c>Failed</c>: a change
/// has reached the screen once it reads <c>Completed</c>. <paramref name="Screens"/> lists the screens playing
/// this slideshow now (full response only); <paramref name="ScreenCount"/> is the same count on list results.
/// </summary>
public sealed record Slideshow(
    string Id,
    string Title,
    Resolution Resolution,
    IReadOnlyList<Slide>? Slides = null,
    string? PlaybackModeOverride = null,
    string EffectivePlaybackMode = "VideoStream",
    int? DefaultDurationSeconds = null,
    Ticker? Ticker = null,
    Clock? Clock = null,
    bool? Shared = null,
    string? RenderStatus = null,
    DateTimeOffset? LastRenderedAt = null,
    IReadOnlyList<Screen>? Screens = null,
    int? ScreenCount = null);

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
/// Every other field is optional; omitted means unchanged.
/// </summary>
public sealed record UpdateSlideshowRequest(
    string? Title = null,
    Resolution? Resolution = null,
    IReadOnlyList<string>? SlideOrder = null,
    string? PlaybackMode = null,
    int? DefaultDurationSeconds = null,
    Ticker? Ticker = null,
    Clock? Clock = null,
    bool? Shared = null);

/// <summary>
/// Request body for <c>PATCH /v1/slides/{id}</c>. Omitted fields are left unchanged. The last five
/// fields apply to dynamic slides only; <paramref name="OverrideData"/> is a merge patch (a null
/// value removes a field) and <paramref name="Countdown"/> of <c>[]</c> clears the countdowns.
/// </summary>
public sealed record UpdateSlideRequest(
    double? DurationSeconds = null,
    bool? Hidden = null,
    string? TemplateId = null,
    string? SourceId = null,
    JsonElement? FieldMapping = null,
    JsonElement? OverrideData = null,
    JsonElement? Countdown = null);

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
/// Request body for <c>POST /v1/slideshows/{id}/slides/dynamic</c>. For a push template
/// (<c>dataMode: "push"</c>) the backend creates a Push source for the slide and returns its id;
/// <see cref="CreatePushKey"/> also returns a push-only key for it. Other templates start with no
/// source; push their data with <c>source update</c>.
/// </summary>
public sealed record AddDynamicSlideRequest(
    string TemplateId,
    double? DurationSeconds = null,
    int? Position = null,
    bool? CreatePushKey = null);

/// <param name="Conditions">The conditions; an empty list clears them.</param>
/// <param name="Mode"><c>show</c> (default) or <c>hide</c>: what happens while the conditions hold.</param>
/// <param name="Match"><c>all</c> (default) or <c>any</c> of the conditions.</param>
public sealed record SetConditionsRequest(
    IReadOnlyList<SlideCondition> Conditions, string? Mode = null, string? Match = null);

public sealed record Template(
    string Id,
    string Title,
    JsonElement? SampleJson = null,
    string? Html = null,
    string? Css = null);

/// <param name="DataMode">
/// <c>"push"</c> for a template whose data an outside system pushes: adding it to a slideshow creates
/// a Push source, and the slide stays hidden until the first data arrives. Omit otherwise.
/// </param>
public sealed record CreateTemplateRequest(
    string Title,
    string? Prompt = null,
    JsonElement? SampleJson = null,
    string? Html = null,
    string? Css = null,
    string? DataMode = null);

/// <summary>Result of <c>POST /v1/sources/{id}/data</c>.</summary>
/// <param name="ReceivedAt">When the data was stored; null for a dry run.</param>
public sealed record PushReceived(DateTimeOffset? ReceivedAt = null);

/// <summary>Result of <c>GET /v1/sources/{id}</c>: the state of a Push source.</summary>
/// <param name="Id">Source id.</param>
/// <param name="Name">Display name.</param>
/// <param name="LastReceivedAt">When data last arrived; null before the first push.</param>
/// <param name="HideAfterMinutes">Data older than this hides the slides; null for never.</param>
/// <param name="IsShowingData">True when data has arrived and has not gone stale.</param>
/// <param name="SlideIds">The slides this source feeds.</param>
public sealed record PushSourceStatus(
    string Id,
    string Name,
    DateTimeOffset? LastReceivedAt,
    int? HideAfterMinutes,
    bool IsShowingData,
    IReadOnlyList<string> SlideIds);

/// <summary>Request body for <c>POST /v1/sources/{id}/keys</c>.</summary>
/// <param name="Name">A label, e.g. the system that will use the key.</param>
public sealed record CreatePushKeyRequest(string? Name = null);

/// <summary>Result of <c>POST /v1/sources/{id}/keys</c>: a push-only key, returned once.</summary>
/// <param name="Id">Key id (revoke with <c>DELETE /v1/apikeys/{id}</c>).</param>
/// <param name="Name">Label.</param>
/// <param name="KeyPrefix">The first characters, to recognise it later.</param>
/// <param name="Key">The full key. Not stored anywhere; shown once.</param>
public sealed record PushKey(string Id, string Name, string KeyPrefix, string Key);

public sealed record GalleryItem(
    string Id,
    string Title,
    string? Description,
    string? PreviewUrl,
    Resolution? Resolution);
