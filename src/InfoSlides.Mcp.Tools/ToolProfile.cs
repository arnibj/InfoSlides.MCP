// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

namespace InfoSlides.Mcp.Tools;

/// <summary>Which set of tools a caller is offered.</summary>
public enum ToolProfile
{
    /// <summary>Every tool. Used by the local stdio server, API-key callers and Gemini.</summary>
    Full,

    /// <summary>
    /// The curated profile for hosted directories (ChatGPT, Claude, Microsoft 365 Copilot). It leaves out anything
    /// that sells, creates accounts, manages credentials, reads the caller's disk, or handles precise location, and
    /// keeps the tool count down: directory reviewers reject commerce and a fuzzy tool surface.
    /// </summary>
    Hosted,
}

/// <summary>
/// The one place that says what the hosted profile contains. It is an allow-list, so a tool added later is
/// hidden from hosted callers until someone adds it here on purpose. Reviewed with the product owner.
/// </summary>
public static class HostedToolProfile
{
    /// <summary>The tools a hosted caller may list and call.</summary>
    public static readonly IReadOnlySet<string> ToolNames = new HashSet<string>(StringComparer.Ordinal)
    {
        // Screens
        "list_devices", "get_device_status", "identify_devices", "pair_device", "create_device", "update_device",
        // show_media_on_device stays on purpose (product owner decision, 2026-10-01).
        "get_device_diagnosis", "get_now_slide_png", "show_media_on_device", "get_show_status", "play_slideshow_find_device",
        // Schedules and takeovers
        "get_schedule", "add_schedule_entry", "assign_schedule", "delete_schedule_entry", "create_takeover", "end_takeover",
        // Slideshows and slides
        "list_slideshows", "get_slideshow", "update_slideshow", "delete_slideshow", "clone_slideshow", "list_gallery",
        "add_media_slide", "add_designed_slide", "add_dynamic_slide", "update_slide", "delete_slide", "set_slide_conditions", "preview_slide",
        "make_ai_slide", "get_ai_slide_job", "insert_ai_slides",
        // Live data
        "list_sources", "get_source_status", "push_data", "update_source",
        // Workspace
        "get_tenant_info", "get_workspace_health", "undo_change",
        // Provided by the host itself (not a CLI tool): who is connected, for multi-account support.
        "get_user_profile",
    };

    /// <summary>The <c>surface</c> claim of Muse clients.</summary>
    public const string MuseSurface = "muse";

    /// <summary>
    /// Hosted tools Muse callers are not offered: Muse's data processing answer says no connector data reaches an AI
    /// model, and these tools send the person's text or files to AI model providers.
    /// </summary>
    public static readonly IReadOnlySet<string> MuseExcluded = new HashSet<string>(StringComparer.Ordinal)
    {
        "make_ai_slide", "get_ai_slide_job", "insert_ai_slides",
    };

    /// <summary>Whether a hosted caller on this surface may list and call the tool.</summary>
    /// <param name="tool">The tool name.</param>
    /// <param name="surface">The token's <c>surface</c> claim, if any.</param>
    /// <returns>True when the tool is in the hosted profile and not excluded for the surface.</returns>
    public static bool IsOffered(string tool, string? surface) =>
        ToolNames.Contains(tool) &&
        !(string.Equals(surface, MuseSurface, StringComparison.OrdinalIgnoreCase) && MuseExcluded.Contains(tool));

    /// <summary>
    /// Parameters removed from a hosted tool's schema and refused when sent: precise location and credential creation
    /// (a push key). The API refuses them for hosted surfaces too; hiding them stops the model offering what it cannot use.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> HiddenParameters =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["list_devices"] = new HashSet<string>(StringComparer.Ordinal) { "near" },
            ["update_device"] = new HashSet<string>(StringComparer.Ordinal) { "latitude", "longitude", "locationName" },
            // A push key outlives the connection (revoking the app does not revoke the key), so a hosted caller may not mint one.
            ["add_dynamic_slide"] = new HashSet<string>(StringComparer.Ordinal) { "createPushKey" },
        };

    /// <summary>
    /// Hosted wording for every hosted tool: it says what the tool does, takes and returns, with no instructions to the
    /// model and no mention of other tools (the directory attestation requires that). The local stdio server keeps the
    /// original text; hosted-rules.json decides what the hosted text may say.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DescriptionOverrides =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["preview_slide"] =
                "Returns a PNG image of how one slide will look on the screen.",
            ["get_device_diagnosis"] =
                "Lists the causes that could explain a screen looking wrong, most likely first: offline since a time, an empty or failed slideshow, nothing scheduled, every slide hidden by its rules, expired plan, or paired elsewhere. Each cause has a plain sentence and, where one exists, a ready-made fix request. An empty list means the screen looks healthy.",
            ["list_devices"] =
                "Lists every screen registered to the workspace with the shape each is set up for, what it is playing now (nowPlayingTitle and nowPlayingSlideshowId) and how many of the allowed screens are used. q filters by name, case and accent insensitive ('lobby' finds 'Lobby Screen'). supportsHtml says whether the screen's app plays HTML playback.",
            ["add_dynamic_slide"] =
                "Adds a slide that keeps itself up to date instead of showing a fixed picture, such as today's soup, a queue number, sales figures or the weather. Takes the templateId of a live-data slide already in a slideshow. With a push template the result includes sourceId, the push source the data is sent to. The slide stays hidden until the first data arrives.",
            ["get_ai_slide_job"] =
                "Returns the state of an AI slide job: Processing, Ready (with preview images), Inserted, NeedsClarification (with a question) or Failed. Jobs are kept for 24 hours.",
            ["identify_devices"] =
                "Shows each online screen's own name in large text (or only the given ones) for about 90 seconds, so a person in the room can see which screen is which. The result lists each screen's deviceId and name, and how many seconds, up to 60, the name takes to appear.",
            ["get_now_slide_png"] =
                "Returns the slide a screen is scheduled to show right now as an image, worked out from its schedule, takeovers, slide rules and slide durations. It shows what is scheduled, not a camera view, so an offline TV may show something else.",
            ["delete_slide"] =
                "Removes one slide from a slideshow permanently. The slides after it close up and any display rule on it is removed.",
            ["list_slideshows"] =
                "Lists everything the workspace can put on a screen, with the shape (landscape or portrait) each is built for, whether its latest render has finished (renderStatus) and how many screens play it (screenCount). q filters by title, case and accent insensitive ('q1' finds 'Q1 Results 2026').",
            ["show_media_on_device"] =
                "Puts a photo or video on a screen as a temporary takeover, then the normal schedule returns. Takes mediaUrl, the public address of the photo or video. The screen switches once processing finishes (seconds for a photo, longer for video). The result carries the ids that identify the upload. A photo whose shape does not match the screen gets a blurred fill, not black bars.",
            ["add_media_slide"] =
                "Adds a picture or video slide to a slideshow, from a publicly reachable URL (downloaded server-side) or the id of a file already in the media library; exactly one is given. If the picture's shape does not match the screen's, the call succeeds and returns an AspectMismatch warning.",
            ["add_designed_slide"] =
                "Adds a text slide to a slideshow, drawn at the screen's size from a heading, optional text and call to action, and a background (a hex colour, an image already in the media library, or an image URL; the workspace accent colour when none is given), optionally with a separate picture beside the text and the workspace logo. The words appear exactly as given. Text over a limit is refused with the field named. Returns the slide id, a previewUrl image of it and the layout used.",
            ["get_schedule"] =
                "Returns a screen's schedule: its default content and any timed entries (such as a breakfast menu 6 to 11), in the workspace time zone, each with an entry id.",
            ["set_slide_conditions"] =
                "Makes a slide appear or vanish by rule, such as the breakfast menu before 11, a weekend offer on Saturday and Sunday, or a message only when a number is hit. Condition types: 'time' (e.g. '08:00-11:00'), 'weekday' (e.g. 'sat,sun'), 'date' (e.g. '2026-12-01..2026-12-26'; either end may be left out), 'data_trigger' (e.g. 'sales_today > 1000000'). By default the slide shows only while all conditions hold; match='any' shows it while at least one holds, and mode='hide' hides it while they hold. Conditions are checked server-side as the stream renders. An empty list clears the slide's rule. Rules marked readOnly were set on the web page and are not replaced by this call.",
            ["pair_device"] =
                "Connects a physical TV to the workspace. The TV shows a pairing screen (the InfoSlides TV app, or https://infoslides.app/pair.html in its browser) with a QR code and a short nickname such as swift-oak-42. Send qr or nickname plus slideshowId (a new screen playing it) or deviceId (replaces the TV on an existing screen). With neither, the result is NeedsClarification listing the workspace's slideshows and screens as choices. With deviceId alone, it returns a 6-character code to type on the TV before it expires. HTML playback is supported only in the browser and the Android app 1.2.0 and later for now.",
            ["update_slideshow"] =
                "Changes a slideshow: rename it, switch between landscape and portrait, reorder the slides (slideOrder is the complete list of slide ids in the wanted order), turn the news ticker or the on-screen clock on or off, set one duration for every slide, or force how it plays. playbackMode overrides the usual rendered-video stream with a smooth HTML/CSS loop, or back again; 'inherit' drops the override. Only what is passed changes. A screen picks up the change after a re-render, when renderStatus is Completed.",
            ["delete_slideshow"] =
                "Deletes a slideshow permanently. Its schedules are removed and any screen playing it stops showing it at its next check-in. There is no undelete.",
            ["undo_change"] =
                "Reverts an earlier change. Calls that change something return an undo field; its body.token is what this takes, within 24 hours. A deleted slide comes back; an added slide, takeover or paired screen is removed. Not covered: deleting a slideshow, and screen settings. If the same thing was changed again since, the result is NeedsClarification, and force=true overwrites that later change.",
            ["create_takeover"] =
                "Takes over one or more screens for a while, such as a fire drill notice for 30 minutes or a launch announcement until 17:00, then returns them to their schedule by themselves. Takes deviceIds, the slideshow, and either durationMinutes or endsAt. Without an end it runs until it is ended.",
            ["list_sources"] =
                "Lists the data sources the workspace can show on a screen: RSS news feeds, calendars, weather and live data pushed by another system. Each entry has its name, type and when it last received data.",
            ["end_takeover"] =
                "Ends a takeover now; the screen goes back to its schedule within about a minute.",
            ["play_slideshow_find_device"] =
                "Plays a slideshow on a screen in one call. Checks that the slideshow has rendered, whether the screen is online and what it plays now, then does it. Without deviceId it picks the only screen, or the one already playing the slideshow; otherwise the result is NeedsClarification listing the screens. With until, it plays as a temporary takeover and the normal schedule returns afterwards; without it, it replaces the screen's default content. dryRun reports what would happen without changing anything.",
            ["add_schedule_entry"] =
                "Plays a slideshow on a screen during a daily time window, such as a breakfast menu from 06:00 to 11:00, on top of the screen's default content. Times are HH:mm in the workspace time zone. Higher priority wins where windows overlap. Warnings (aspect mismatch, HTML unsupported on this screen) come back with the result.",
            ["assign_schedule"] =
                "Sets what a screen plays: connects slideshows to a registered screen, in the order given, as a continuous loop. The call succeeds even when the content's shape does not match the screen's, and returns an AspectMismatch warning in that case.",
            ["get_tenant_info"] =
                "Returns the workspace name, owner, whether their email is confirmed, which plan it is on, how many screens are in use out of the allowance, and the scope of the credential in use.",
            ["update_device"] =
                "Renames a screen or changes its resolution (1080x1920 for a screen turned on its end). Only what is sent changes.",
            ["push_data"] =
                "Sends new data to a push source: the whole set of values the slide shows, as one JSON object with the template's field names. Every slide on the source updates, and a slide that was hidden waiting for data appears. Returns receivedAt. dryRun=true checks the data without storing it.",
            ["insert_ai_slides"] =
                "Adds the previews of a Ready AI slide job to the slideshow. Repeating the call does not add them twice. Queues a re-render.",
            ["get_source_status"] =
                "Returns the status of a source. For a push source: when data last arrived (lastReceivedAt), whether its slides are showing data (isShowingData, false before the first push or after the data went stale), the staleness timeout, and which slides it feeds. For a fetched source (RSS, weather and so on): lastFetchedAt, with the push fields empty.",
            ["get_device_status"] =
                "Returns whether a screen is on and what it is showing right now: whether the TV is live, when it last checked in and which content is playing.",
            ["get_show_status"] =
                "Returns the progress of a photo or video sent to a screen: one combined percent for video processing and the slideshow render, and a status of Processing, Ready or Failed.",
            ["get_slideshow"] =
                "Returns one slideshow in full: every slide (its type, whether it is hidden, how long it is shown, a thumbnailUrl and every rule about when it appears), the order they play in, the ticker, clock and default duration, which screens play it (each with a playerUrl) and renderStatus. A change is on the screen once renderStatus is Completed.",
            ["delete_schedule_entry"] =
                "Removes one timed entry from a screen's schedule. The screen falls back to its default content in that window.",
            ["update_source"] =
                "Sends fresh values for a live-data slide, such as today's menu, a price, a total or opening hours. The display re-renders itself server-side. The data uses the fields the slide's template expects. For a slide on a push source the data goes to that source. dryRun=true checks the data without changing what is on screen.",
            ["clone_slideshow"] =
                "Copies an existing slideshow so it can be changed without touching the original, or, with fromGallery=true, copies a ready-made design from the gallery.",
            ["update_slide"] =
                "Changes one slide already in a slideshow: how long it stays on screen (durationSeconds, 1 to 300) and whether it is hidden (hidden). For a live-data slide it can also switch to another template (templateId) or content source (sourceId), change values on it (overrideData, e.g. {\"price\":\"1.990 kr\"}; only the fields sent change, and null removes one), or set its countdown. Text inside an uploaded PowerPoint page cannot be edited this way. Only what is passed changes.",
            ["list_gallery"] =
                "Lists ready-made screen designs, such as menu boards, welcome screens and notice layouts, that can be copied into the workspace.",
            ["get_workspace_health"] =
                "Returns a summary of the workspace in one call: a sentence such as '11 of 12 screens are online; Lobby has been offline since 09:12', every screen with online and lastSeenAt, usage against the plan, and problems most urgent first (offline screens, failed renders, slideshows no screen plays, expired plan, limits near full), each with a code, a message and a ready-made fix. Empty problems means all is well.",
            ["create_device"] =
                "Registers the physical screen the content will play on, such as the TV in reception or a menu board (Icelandic: upplýsingaskjár, skjár). Once per screen. The workspace's plan sets how many screens can be active; DeviceLimitReached means they are all in use, and a workspace admin can change the plan in InfoSlides account settings. Resolution defaults to 1920x1080; 1080x1920 is for a screen turned on its end.",
            ["make_ai_slide"] =
                "AI Studio: designs a slide in the workspace's style from a description (handler prompt: headline, details, prices, times), a photo of a poster, menu or whiteboard (photo: mediaUrl, or mediaAssetId of a file in the media library), a web page (url) or a PDF/Word file (document: mediaAssetId). Returns a job at once; the job moves from Processing to Ready with preview images, taking 10 to 60 seconds. EntitlementRequired means AI Studio is not on the workspace's plan; a workspace admin can change the plan in InfoSlides account settings.",
        };

    /// <summary>
    /// Hosted wording for single parameters whose shared description points at a tool the hosted profile does not
    /// offer. Keyed by tool, then parameter.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ParameterDescriptionOverrides =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["add_dynamic_slide"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["templateId"] = "Id of a template (the templateId of an existing live-data slide).",
            },
            ["add_media_slide"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mediaAssetId"] = "Id of a file already in the media library; omit when using mediaUrl.",
            },
            ["update_slide"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["templateId"] = "Dynamic slides only: id of the template to switch to.",
                ["sourceId"] = "Dynamic slides only: id of the content source to take data from.",
            },
            ["update_slideshow"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["tickerSourceIds"] = "Content sources whose headlines scroll in the ticker.",
            },
            ["get_ai_slide_job"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["jobId"] = "Id of the AI slide job.",
            },
            ["get_show_status"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mediaAssetId"] = "The mediaAssetId of the uploaded photo or video.",
                ["slideshowId"] = "The slideshowId of the slideshow created for it.",
            },
            ["set_slide_conditions"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["match"] = "\"all\" (default): every condition holds. \"any\": one is enough.",
            },
            ["make_ai_slide"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["prompt"] = "The text of the slide (handler prompt).",
                ["backgroundDescription"] = "With background photo: the subject of the photo.",
            },
            ["undo_change"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["force"] = "True to overwrite a later change.",
            },
            ["push_data"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceId"] = "Id of the push source.",
            },
        };

    /// <summary>
    /// Picks the profile for a caller. API-key callers and Gemini (which has no commerce ban) get every tool; any
    /// other OAuth caller, including a client with no surface, gets the hosted profile.
    /// </summary>
    /// <param name="isApiKey">True when the caller authenticated with an <c>isk_</c> key.</param>
    /// <param name="surface">The token's <c>surface</c> claim, if any.</param>
    /// <returns>The profile to apply.</returns>
    public static ToolProfile ProfileFor(bool isApiKey, string? surface) =>
        isApiKey || string.Equals(surface, "gemini", StringComparison.OrdinalIgnoreCase)
            ? ToolProfile.Full
            : ToolProfile.Hosted;
}
