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
        "add_media_slide", "add_dynamic_slide", "update_slide", "delete_slide", "set_slide_conditions", "preview_slide",
        "make_ai_slide", "get_ai_slide_job", "insert_ai_slides",
        // Live data
        "list_sources", "get_source_status", "push_data", "update_source",
        // Workspace
        "get_tenant_info", "get_workspace_health", "undo_change",
        // Provided by the host itself (not a CLI tool): who is connected, for multi-account support.
        "get_user_profile",
    };

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
    /// Hosted wording for tools whose shared description mentions upgrades, trials or tools the hosted profile does
    /// not offer. The local stdio server keeps the original text; hosted-rules.json decides what the hosted text may say.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> DescriptionOverrides =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["create_device"] =
                "Register the physical screen the content will play on — the TV in reception, the " +
                "menu board above the counter, the noticeboard in the corridor, the display in the " +
                "waiting room, the monitor in the shop window (Icelandic: upplýsingaskjár, skjár). " +
                "Do this once per screen. The workspace's plan sets how many screens can be active; " +
                "DeviceLimitReached means they are all in use, and a workspace admin can change the plan in " +
                "InfoSlides account settings. Resolution defaults to 1920x1080 for a normal wall-mounted TV — " +
                "use 1080x1920 for a screen turned on its end, which is common for menu boards and window displays.",
            ["make_ai_slide"] =
                "AI Studio: design a slide in the workspace's style from a description (handler prompt: " +
                "headline, details, prices, times), a photo of a poster, menu or whiteboard (photo: " +
                "mediaUrl, or mediaAssetId of a file already in the media library), a web page (url) or a " +
                "PDF/Word file (document: mediaAssetId). Returns a job at once: poll get_ai_slide_job every 5 " +
                "seconds (10 to 60 seconds). By default it stops at Ready with preview images: show them to the " +
                "person, then insert_ai_slides. To show a photo as it is, use add_media_slide instead. " +
                "EntitlementRequired means AI Studio is not on the workspace's plan; a workspace admin can change " +
                "the plan in InfoSlides account settings.",
            // Template ids come from slides already in the workspace: list_templates and create_template are not hosted.
            ["add_dynamic_slide"] =
                "Add a slide that keeps itself up to date instead of showing a fixed picture — " +
                "today's soup, the current queue number, live sales figures, tomorrow's weather. " +
                "Needs a template id: use the templateId of a live-data slide already in a slideshow " +
                "(get_slideshow lists it), for example one in a gallery design copied with clone_slideshow. " +
                "New templates are designed in InfoSlides itself. With a push template the result includes " +
                "sourceId, the push source to send data to with push_data; the slide stays hidden until the " +
                "first data arrives. Otherwise push values with update_source.",
            ["add_media_slide"] =
                "Put a picture or a video on the screen — a photo of the specials board, a poster, a " +
                "promo clip, a logo. Takes either a publicly reachable URL (downloaded server-side) or " +
                "the id of a file already in the workspace's media library; provide exactly " +
                "one. If the picture's shape does not match the screen's, the call still succeeds but " +
                "returns an AspectMismatch warning — fix it rather than letting content be stretched " +
                "or cropped on a display the public can see.",
            // get_stream_link is not hosted; get_slideshow hands out each screen's playerUrl.
            ["assign_schedule"] =
                "Tell a screen what to play. Connects slideshows to a registered display, in the " +
                "order given, as a continuous loop. The call succeeds even when the content's shape " +
                "does not match the screen's, but returns an AspectMismatch warning — act on it, " +
                "because it means the content will be stretched or cropped on a display people can " +
                "see. To get the TV showing it, use the playerUrl that get_slideshow lists for the " +
                "screen, or connect a TV that shows its pairing screen with pair_device.",
            ["update_slide"] =
                "Change one slide that is already in a slideshow: make it stay on screen longer or " +
                "shorter (durationSeconds, 1 to 300), or hide it and bring it back (hidden), like \"hide " +
                "the Christmas slide\", \"show the offer for 20 seconds\". For a live-data slide it " +
                "can also change what it shows: switch to another template (templateId) or content " +
                "source (sourceId), change the value on it (overrideData, e.g. {\"price\":\"1.990 kr\"}; " +
                "only the fields you send change, and null removes one), or set its countdown. Text " +
                "inside an uploaded PowerPoint page cannot be edited this way: say so, and suggest " +
                "replacing the file in InfoSlides. Only what you pass changes. To take " +
                "the slide out for good use delete_slide.",
            ["update_source"] =
                "Put fresh information on the screen: send today's menu, the new price, the current " +
                "total, the updated opening hours. The display re-renders itself server-side — nobody " +
                "has to touch the TV. The data must use the fields the slide's template expects: send " +
                "the same field names the slide already shows (preview_slide shows it). For a slide on a " +
                "push source the data goes to that source, exactly as push_data would send it. Set " +
                "dryRun=true to check the data without changing what is on screen.",
            // The hosted server cannot read the person's files, so only mediaUrl is offered.
            ["show_media_on_device"] =
                "\"Show this photo on the lobby screen for an hour.\" Puts a photo or video on a screen as " +
                "a temporary takeover, then the normal schedule returns. Give mediaUrl (a public address of the " +
                "photo or video). The screen switches once processing finishes (seconds for a photo, longer for " +
                "video): poll get_show_status with the returned ids and report the percent. A photo whose shape " +
                "does not match the screen gets a blurred fill, not black bars.",
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
                ["templateId"] = "Id of a template (the templateId of an existing live-data slide, from get_slideshow).",
            },
            ["add_media_slide"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["mediaAssetId"] = "Id of a file already in the media library; omit when using mediaUrl.",
            },
            ["update_slide"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["templateId"] = "Dynamic slides only: id of the template to switch to (the templateId of another live-data slide, from get_slideshow).",
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
