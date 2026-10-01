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
