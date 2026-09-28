using System.ComponentModel;
using InfoSlides.Core.Api;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static InfoSlides.Cli.Tools.ToolResults;

namespace InfoSlides.Cli.Tools;

/// <summary>
/// Screen-side tools on the pass-through client: pairing a TV, playing something now, timed
/// schedules, temporary takeovers, play-time reports and a screen's saved location. Results are the
/// API's own JSON, so new fields reach the agent without a release of this server.
/// </summary>
[McpServerToolType]
public sealed class ScreenTools(InfoSlidesApiClient api)
{
    private static string Id(string id) => Uri.EscapeDataString(id);

    [McpServerTool(Name = "pair_screen")]
    [Description("Connect a physical TV to this workspace. The TV shows a pairing screen (the InfoSlides " +
                 "TV app, or https://infoslides.app/pair.html in its browser) with a QR code and a short " +
                 "nickname such as swift-oak-42. If the person reads you the QR code or nickname, or you " +
                 "see it through their glasses, send qr or nickname plus slideshowId (a new screen playing " +
                 "it) or deviceId (replaces the TV on an existing screen). With neither, the error is " +
                 "NeedsClarification with the workspace's slideshows and screens as choices: call again " +
                 "with the chosen id. With nothing to scan, send deviceId alone: you get a 6-character code " +
                 "for the person to type on the TV before it expires. For live pushed data (HTML playback), " +
                 "prefer the browser page: only the browser and the Android app 1.2.0+ play HTML for now.")]
    public Task<CallToolResult> PairScreen(
        [Description("The scanned QR code URL (or its bare session id).")] string? qr = null,
        [Description("The nickname shown under the QR code, e.g. 'swift-oak-42'.")] string? nickname = null,
        [Description("An existing screen: alone, returns a code to type on the TV; with qr/nickname, binds this TV to it.")] string? deviceId = null,
        [Description("With qr/nickname: creates a new screen playing this slideshow.")] string? slideshowId = null,
        [Description("Name for a newly created screen, e.g. 'Lobby TV'.")] string? deviceName = null,
        [Description("True to validate without pairing anything.")] bool dryRun = false,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, "/v1/pairings" + InfoSlidesApiClient.Query(("dryRun", dryRun ? "true" : null)),
            Body(("qr", qr), ("nickname", nickname), ("deviceId", deviceId), ("slideshowId", slideshowId), ("deviceName", deviceName)),
            idempotent: true, ct));

    [McpServerTool(Name = "play_now")]
    [Description("\"Play my Q1 slides on the lobby screen\" in one call. Checks that the slideshow has " +
                 "rendered, whether the screen is online and what it plays now, then does it. Without " +
                 "deviceId it picks the only screen, or the one already playing the slideshow; otherwise the " +
                 "error is NeedsClarification listing the screens to choose from. With until, it plays as a " +
                 "temporary takeover and the normal schedule returns afterwards; without it, it replaces the " +
                 "screen's default content. Use dryRun to tell the person what would happen first.")]
    public Task<CallToolResult> PlayNow(
        [Description("The slideshow to play.")] string slideshowId,
        [Description("The screen; optional when the workspace has one screen.")] string? deviceId = null,
        [Description("Optional end time, e.g. '2026-10-01T17:00' (workspace local time) or with Z/offset. Makes it a takeover.")] string? until = null,
        [Description("True to report what would happen without changing anything.")] bool dryRun = false,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, "/v1/play" + InfoSlidesApiClient.Query(("dryRun", dryRun ? "true" : null)),
            Body(("slideshowId", slideshowId), ("deviceId", deviceId), ("until", until)), ct: ct));

    [McpServerTool(Name = "get_schedule", ReadOnly = true)]
    [Description("See a screen's schedule: its default content and any timed entries (breakfast menu " +
                 "6-11, lunch menu 11-15), in the workspace time zone. Use the entry ids with " +
                 "delete_schedule_entry.")]
    public Task<CallToolResult> GetSchedule(
        [Description("Id of the screen.")] string deviceId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, $"/v1/devices/{Id(deviceId)}/schedule", ct: ct));

    [McpServerTool(Name = "add_schedule_entry")]
    [Description("Play a slideshow on a screen during a time window every day, e.g. the breakfast menu " +
                 "from 06:00 to 11:00, on top of the screen's default content. Times are HH:mm in the " +
                 "workspace time zone. Higher priority wins where windows overlap. Warnings (aspect " +
                 "mismatch, HTML unsupported on this screen) come back with the result: pass them on.")]
    public Task<CallToolResult> AddScheduleEntry(
        [Description("Id of the screen.")] string deviceId,
        [Description("The slideshow to play in the window.")] string slideshowId,
        [Description("Start time, HH:mm, e.g. '06:00'.")] string startTime,
        [Description("End time, HH:mm, e.g. '11:00'.")] string endTime,
        [Description("Priority where windows overlap (default 0).")] int priority = 0,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, $"/v1/devices/{Id(deviceId)}/schedule/entries",
            Body(("slideshowId", slideshowId), ("startTime", startTime), ("endTime", endTime), ("priority", priority)),
            idempotent: true, ct));

    [McpServerTool(Name = "delete_schedule_entry", Destructive = true)]
    [Description("Remove one timed entry from a screen's schedule (ids from get_schedule). The screen " +
                 "falls back to its default content in that window.")]
    public Task<CallToolResult> DeleteScheduleEntry(
        [Description("Id of the screen.")] string deviceId,
        [Description("Id of the schedule entry.")] string entryId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Delete, $"/v1/devices/{Id(deviceId)}/schedule/entries/{Id(entryId)}", ct: ct));

    [McpServerTool(Name = "create_takeover")]
    [Description("Take over one or more screens for a while, e.g. a fire drill notice for 30 minutes or " +
                 "a launch announcement on every screen until 17:00, then return them to their schedule by " +
                 "themselves. Give deviceIds (one or more screens), the slideshow, and either " +
                 "durationMinutes or endsAt. Without an end it runs until end_takeover.")]
    public Task<CallToolResult> CreateTakeover(
        [Description("Ids of the screens to take over.")] List<string> deviceIds,
        [Description("The slideshow to show.")] string slideshowId,
        [Description("How long, in minutes.")] int? durationMinutes = null,
        [Description("Optional start, e.g. '2026-10-01T09:00' (workspace local time) or with Z/offset; default now.")] string? startsAt = null,
        [Description("Optional end, read like startsAt.")] string? endsAt = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, "/v1/takeovers",
            Body(("deviceIds", new System.Text.Json.Nodes.JsonArray(deviceIds.Select(d => (System.Text.Json.Nodes.JsonNode?)d).ToArray())),
                ("slideshowId", slideshowId), ("durationMinutes", durationMinutes), ("startsAt", startsAt), ("endsAt", endsAt)),
            idempotent: true, ct));

    [McpServerTool(Name = "list_takeovers", ReadOnly = true)]
    [Description("See temporary takeovers: what is interrupting a screen's schedule right now, or " +
                 "planned. Filter by screen, or only the active ones.")]
    public Task<CallToolResult> ListTakeovers(
        [Description("Only this screen's takeovers.")] string? deviceId = null,
        [Description("Only takeovers active right now.")] bool activeOnly = false,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get,
            "/v1/takeovers" + InfoSlidesApiClient.Query(("deviceId", deviceId), ("activeOnly", activeOnly ? "true" : null)), ct: ct));

    [McpServerTool(Name = "end_takeover", Destructive = true)]
    [Description("End a takeover now; the screen goes back to its schedule within about a minute.")]
    public Task<CallToolResult> EndTakeover(
        [Description("Id of the takeover.")] string takeoverId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Delete, $"/v1/takeovers/{Id(takeoverId)}", ct: ct));

    [McpServerTool(Name = "get_play_time", ReadOnly = true)]
    [Description("\"How long did the lobby screen play?\" or \"where did the Q1 slideshow run last week?\" " +
                 "Estimated minutes per day: per slideshow for a screen (deviceId), or per screen for a " +
                 "slideshow (slideshowId). Estimated from screen heartbeats, not exact, and there are no " +
                 "per-slide counts: say so when you report it.")]
    public Task<CallToolResult> GetPlayTime(
        [Description("A screen: report per slideshow it played.")] string? deviceId = null,
        [Description("A slideshow: report per screen it played on.")] string? slideshowId = null,
        [Description("Start date, yyyy-MM-dd.")] string? from = null,
        [Description("End date, yyyy-MM-dd.")] string? to = null,
        CancellationToken ct = default)
    {
        if ((deviceId is null) == (slideshowId is null))
        {
            return Task.FromResult(ValidationError("Give exactly one of deviceId or slideshowId."));
        }

        var path = deviceId is not null ? $"/v1/devices/{Id(deviceId)}/plays" : $"/v1/slideshows/{Id(slideshowId!)}/plays";
        return Json(() => api.SendJsonAsync(HttpMethod.Get, path + InfoSlidesApiClient.Query(("from", from), ("to", to)), ct: ct));
    }

    [McpServerTool(Name = "set_device_location")]
    [Description("Save where a screen is, so \"this screen\" or \"the screen in front of me\" can be found " +
                 "later with list_devices near the person's position. Do it once, right after pairing.")]
    public Task<CallToolResult> SetDeviceLocation(
        [Description("Id of the screen.")] string deviceId,
        [Description("Latitude in degrees.")] double latitude,
        [Description("Longitude in degrees.")] double longitude,
        [Description("Optional label, e.g. 'Front lobby entrance'.")] string? locationName = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Patch, $"/v1/devices/{Id(deviceId)}",
            Body(("latitude", latitude), ("longitude", longitude), ("locationName", locationName)), ct: ct));
}
