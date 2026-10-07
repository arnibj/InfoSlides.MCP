using System.Text.Json.Nodes;
using System.ComponentModel;
using InfoSlides.Core.Api;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static InfoSlides.Mcp.Tools.ToolResults;

namespace InfoSlides.Mcp.Tools;

/// <summary>
/// Screen-side tools on the pass-through client: pairing a TV, playing something now, timed
/// schedules, temporary takeovers, play-time reports and a screen's saved location. Results are the
/// API's own JSON, so new fields reach the agent without a release of this server.
/// </summary>
[McpServerToolType]
public sealed class ScreenTools(InfoSlidesApiClient api)
{
    private static string Id(string id) => Uri.EscapeDataString(id);

    [McpServerTool(Name = "pair_device", Title = "Pair a TV screen", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = false)]
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

    [McpServerTool(Name = "play_slideshow_find_device", Title = "Play slideshow on screen", ReadOnly = false, Destructive = true, OpenWorld = false, Idempotent = true)]
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

    [McpServerTool(Name = "get_schedule", Title = "Get screen schedule entries", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("See a screen's schedule: its default content and any timed entries (breakfast menu " +
                 "6-11, lunch menu 11-15), in the workspace time zone. Say which time zone it is when you read " +
                 "the times out. Use the entry ids with delete_schedule_entry.")]
    public Task<CallToolResult> GetSchedule(
        [Description("Id of the screen.")] string deviceId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, $"/v1/devices/{Id(deviceId)}/schedule", ct: ct));

    [McpServerTool(Name = "add_schedule_entry", Title = "Add schedule entry", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = false)]
    [Description("Play a slideshow on a screen during a time window every day, e.g. the breakfast menu " +
                 "from 06:00 to 11:00, on top of the screen's default content. Times are HH:mm in the " +
                 "workspace time zone: name it when you confirm. For a one-off stretch (\"for two hours\", " +
                 "\"until 17:00\") use create_takeover instead. Higher priority wins where windows overlap. Warnings (aspect " +
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

    [McpServerTool(Name = "delete_schedule_entry", Title = "Delete schedule entry", ReadOnly = false, Destructive = true, OpenWorld = false, Idempotent = true)]
    [Description("Remove one timed entry from a screen's schedule (ids from get_schedule). The screen " +
                 "falls back to its default content in that window.")]
    public Task<CallToolResult> DeleteScheduleEntry(
        [Description("Id of the screen.")] string deviceId,
        [Description("Id of the schedule entry.")] string entryId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Delete, $"/v1/devices/{Id(deviceId)}/schedule/entries/{Id(entryId)}", ct: ct));

    [McpServerTool(Name = "create_takeover", Title = "Start screen takeover", ReadOnly = false, Destructive = true, OpenWorld = false, Idempotent = false)]
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

    [McpServerTool(Name = "list_takeovers", Title = "List screen takeovers", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("See temporary takeovers: what is interrupting a screen's schedule right now, or " +
                 "planned. Filter by screen, or only the active ones.")]
    public Task<CallToolResult> ListTakeovers(
        [Description("Only this screen's takeovers.")] string? deviceId = null,
        [Description("Only takeovers active right now.")] bool activeOnly = false,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get,
            "/v1/takeovers" + InfoSlidesApiClient.Query(("deviceId", deviceId), ("activeOnly", activeOnly ? "true" : null)), ct: ct));

    [McpServerTool(Name = "end_takeover", Title = "End screen takeover", ReadOnly = false, Destructive = true, OpenWorld = false, Idempotent = true)]
    [Description("End a takeover now; the screen goes back to its schedule within about a minute.")]
    public Task<CallToolResult> EndTakeover(
        [Description("Id of the takeover.")] string takeoverId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Delete, $"/v1/takeovers/{Id(takeoverId)}", ct: ct));

    [McpServerTool(Name = "get_slideshow_plays", Title = "Get slideshow play time", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("\"Where did the Q1 slideshow run last week?\" Estimated minutes per day on each screen " +
                 "that played the slideshow, with a total and an approximate loop count. Estimated from " +
                 "screen heartbeats, not exact, and there are no per-slide counts: say so when you report it.")]
    public Task<CallToolResult> GetSlideshowPlays(
        [Description("The slideshow.")] string slideshowId,
        [Description("Start date, yyyy-MM-dd (UTC day).")] string from,
        [Description("End date, yyyy-MM-dd (UTC day).")] string to,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get,
            $"/v1/slideshows/{Id(slideshowId)}/plays" + InfoSlidesApiClient.Query(("from", from), ("to", to)), ct: ct));

    [McpServerTool(Name = "get_device_plays", Title = "Get screen play time", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("\"How long did the lobby screen play, and what?\" Estimated minutes per day for each " +
                 "slideshow the screen played. Estimated from screen heartbeats, not exact: say so when you report it.")]
    public Task<CallToolResult> GetDevicePlays(
        [Description("The screen.")] string deviceId,
        [Description("Start date, yyyy-MM-dd (UTC day).")] string from,
        [Description("End date, yyyy-MM-dd (UTC day).")] string to,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get,
            $"/v1/devices/{Id(deviceId)}/plays" + InfoSlidesApiClient.Query(("from", from), ("to", to)), ct: ct));

    [McpServerTool(Name = "identify_devices", Title = "Identify screens visually", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = false)]
    [Description("\"Which screen is this?\" Shows each online screen's own name in large text (or just the given " +
                 "ones) for about 90 seconds so the person can read out the name they see. The result " +
                 "lists each screen's deviceId and name, and says how many seconds it takes to appear " +
                 "(up to 60): tell the person to wait that long before looking. Try list_devices with near " +
                 "first; use this when several screens are in one room.")]
    public Task<CallToolResult> IdentifyScreens(
        [Description("Only these screens; all online screens when omitted.")] List<string>? deviceIds = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, "/v1/devices/identify",
            Body(("deviceIds", deviceIds is { Count: > 0 } ? new JsonArray(deviceIds.Select(d => (JsonNode?)d).ToArray()) : null)),
            ct: ct));

    [McpServerTool(Name = "get_now_slide_png", Title = "Get current slide preview", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("\"What's on the lobby screen?\" Returns the slide the screen should be showing right now " +
                 "as an image, worked out from its schedule, takeovers, slide rules and slide durations. " +
                 "It is what should be on the screen, not a camera: an offline TV may show something else, " +
                 "so check get_device_status too.")]
    public Task<CallToolResult> GetScreenNow(
        [Description("Id of the screen.")] string deviceId,
        CancellationToken ct = default) =>
        ExecutePng(() => api.GetPngAsync(HttpMethod.Get, $"/v1/devices/{Id(deviceId)}/now.png", null, ct),
            $"What screen {deviceId} should show now");

    [McpServerTool(Name = "get_device_diagnosis", Title = "Diagnose screen issues", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("\"Why is the lobby screen black?\" Lists every cause that could explain a screen looking " +
                 "wrong, most likely first: offline since a time, an empty or failed slideshow, nothing " +
                 "scheduled, every slide hidden by its rules, expired plan, or paired elsewhere. Each cause " +
                 "has a sentence to read out and, where there is one, a ready-made fix request. No causes " +
                 "means the screen looks healthy.")]
    public Task<CallToolResult> DiagnoseScreen(
        [Description("Id of the screen.")] string deviceId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, $"/v1/devices/{Id(deviceId)}/diagnosis", ct: ct));

    [McpServerTool(Name = "show_media_on_device", Title = "Show photo or clip on screen", ReadOnly = false, Destructive = false, OpenWorld = true, Idempotent = false)]
    [Description("\"Show this photo on the lobby screen for an hour.\" Puts a photo or video on a screen as " +
                 "a temporary takeover, then the normal schedule returns. Give filePath (a local file) or " +
                 "mediaUrl (a public address), not both. The screen switches once processing finishes " +
                 "(seconds for a photo, longer for video): poll get_show_status with the returned ids and " +
                 "report the percent. A photo whose shape does not match the screen gets a blurred fill, " +
                 "not black bars.")]
    public async Task<CallToolResult> ShowOnScreen(
        [Description("Id of the screen.")] string deviceId,
        [Description("Absolute path to a photo or video on disk.")] string? filePath = null,
        [Description("Public address of a photo or video to download instead.")] string? mediaUrl = null,
        [Description("How long it stays, e.g. '2026-10-01T17:00' (workspace local time) or with Z/offset; default 30 minutes from now.")] string? until = null,
        [Description("Optional name for the slideshow that is created.")] string? caption = null,
        CancellationToken ct = default)
    {
        if ((filePath is null) == (mediaUrl is null))
        {
            return ValidationError("Give exactly one of filePath or mediaUrl.");
        }

        if (filePath is null)
        {
            return await Json(() => api.ShowOnDeviceAsync(deviceId, null, null, null, mediaUrl, until, caption, ct));
        }

        if (!File.Exists(filePath))
        {
            return ValidationError($"File not found: {filePath}");
        }

        await using var stream = File.OpenRead(filePath);
        return await Json(() => api.ShowOnDeviceAsync(deviceId, stream, Path.GetFileName(filePath),
            MediaTools.ResolveContentType(filePath), null, until, caption, ct));
    }

    [McpServerTool(Name = "get_show_status", Title = "Get show progress", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Check a photo or video sent with show_media_on_device: one combined percent for the video " +
                 "processing and the slideshow render, and status Processing, Ready or Failed. Poll at " +
                 "most every 5 seconds.")]
    public Task<CallToolResult> GetShowStatus(
        [Description("Id of the screen.")] string deviceId,
        [Description("mediaAssetId from the show_media_on_device result.")] string mediaAssetId,
        [Description("slideshowId from the show_media_on_device result.")] string slideshowId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get,
            $"/v1/devices/{Id(deviceId)}/show/{Id(mediaAssetId)}" + InfoSlidesApiClient.Query(("slideshowId", slideshowId)), ct: ct));

    [McpServerTool(Name = "update_device", Title = "Update screen details", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Rename a screen, change its resolution (use 1080x1920 for a screen turned on its end), or " +
                 "save where it is so \"this screen\" can be found later with list_devices near the person's " +
                 "position (do that once, right after pairing). Send only what should change.")]
    public Task<CallToolResult> UpdateDevice(
        [Description("Id of the screen.")] string deviceId,
        [Description("New name, e.g. 'Lobby TV'.")] string? name = null,
        [Description("New width in pixels; give with height.")] int? width = null,
        [Description("New height in pixels; give with width.")] int? height = null,
        [Description("Latitude in degrees; give with longitude.")] double? latitude = null,
        [Description("Longitude in degrees; give with latitude.")] double? longitude = null,
        [Description("Optional label for the location, e.g. 'Front lobby entrance'.")] string? locationName = null,
        CancellationToken ct = default)
    {
        if ((width is null) != (height is null))
        {
            return Task.FromResult(ValidationError("Give width and height together."));
        }

        if ((latitude is null) != (longitude is null))
        {
            return Task.FromResult(ValidationError("Give latitude and longitude together."));
        }

        if (name is null && width is null && latitude is null && locationName is null)
        {
            return Task.FromResult(ValidationError("Give a name, a resolution, a location, or a combination."));
        }

        return Json(() => api.SendJsonAsync(HttpMethod.Patch, $"/v1/devices/{Id(deviceId)}",
            Body(("name", name),
                ("resolution", width is null ? null : new JsonObject { ["width"] = width, ["height"] = height }),
                ("latitude", latitude), ("longitude", longitude), ("locationName", locationName)), ct: ct));
    }

    [McpServerTool(Name = "set_device_offline_alerts", Title = "Set screen offline alerts", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Stop or restart the \"screen went offline\" emails for one screen, e.g. a TV that is " +
                 "switched off on purpose, and optionally give it its own quiet hours. This replaces the " +
                 "screen's settings, so send enabled every time; without quiet hours the workspace's apply.")]
    public Task<CallToolResult> SetScreenOfflineAlerts(
        [Description("Id of the screen.")] string deviceId,
        [Description("False stops the emails for this screen.")] bool enabled,
        [Description("Optional quiet hours start, HH:mm in the workspace time zone; give with quietEnd.")] string? quietStart = null,
        [Description("Optional quiet hours end, HH:mm; before quietStart spans midnight.")] string? quietEnd = null,
        CancellationToken ct = default)
    {
        if ((quietStart is null) != (quietEnd is null))
        {
            return Task.FromResult(ValidationError("Give quietStart and quietEnd together, or neither."));
        }

        return Json(() => api.SendJsonAsync(HttpMethod.Put, $"/v1/devices/{Id(deviceId)}/offline-alerts",
            new JsonObject { ["enabled"] = enabled, ["quietHours"] = Quiet(quietStart, quietEnd) }, ct: ct));
    }

    /// <summary>Builds a <c>{start, end}</c> quiet-hours object, or null (no quiet hours) when neither end is given.</summary>
    internal static JsonNode? Quiet(string? start, string? end) =>
        start is null && end is null ? null : new JsonObject { ["start"] = start, ["end"] = end };
}
