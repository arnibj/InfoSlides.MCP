using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using InfoSlides.Core.Api;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static InfoSlides.Cli.Tools.ToolResults;

namespace InfoSlides.Cli.Tools;

/// <summary>
/// Workspace tools on the pass-through client: the team, workspace settings, and fetching content
/// sources (RSS, weather, calendars). Results are the API's own JSON.
/// </summary>
[McpServerToolType]
public sealed class WorkspaceTools(InfoSlidesApiClient api)
{
    private static string Id(string id) => Uri.EscapeDataString(id);

    [McpServerTool(Name = "list_team", ReadOnly = true)]
    [Description("See who is in this workspace (members and their roles) and which invitations are " +
                 "still waiting to be accepted.")]
    public Task<CallToolResult> ListTeam(CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, "/v1/team", ct: ct));

    [McpServerTool(Name = "invite_team_member")]
    [Description("Invite a person to this workspace by email. This is how the person gets in after you " +
                 "created the workspace with your own address: invite them as TenantAdmin (the default) so " +
                 "they can manage screens, billing and the team themselves. Other roles: ContentManager " +
                 "(edits content), DeviceManager (pairs screens), Viewer. The invitation expires in 7 days.")]
    public Task<CallToolResult> InviteTeamMember(
        [Description("The person's email address.")] string email,
        [Description("TenantAdmin (default), ContentManager, DeviceManager or Viewer.")] string? role = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, "/v1/team/invitations",
            Body(("email", email), ("role", role)), idempotent: true, ct));

    [McpServerTool(Name = "revoke_team_invitation", Destructive = true)]
    [Description("Cancel a pending invitation (ids from list_team) so its link stops working.")]
    public Task<CallToolResult> RevokeInvitation(
        [Description("Id of the invitation.")] string invitationId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Delete, $"/v1/team/invitations/{Id(invitationId)}", ct: ct));

    [McpServerTool(Name = "remove_team_member", Destructive = true)]
    [Description("Remove a person from this workspace (ids from list_team). Confirm with the person first.")]
    public Task<CallToolResult> RemoveTeamMember(
        [Description("Id of the member.")] string memberId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Delete, $"/v1/team/members/{Id(memberId)}", ct: ct));

    [McpServerTool(Name = "update_tenant")]
    [Description("Set the workspace time zone and/or locale. The time zone decides when schedules switch, " +
                 "what TV clocks show and when time-based slide conditions apply; set it early for a " +
                 "workspace outside Iceland. The locale sets number and date formats in rendered slides " +
                 "(changing it re-renders them).")]
    public Task<CallToolResult> UpdateWorkspaceSettings(
        [Description("IANA time zone, e.g. 'Europe/London'.")] string? timeZone = null,
        [Description("Locale, e.g. 'en-GB' or 'is-IS'.")] string? locale = null,
        CancellationToken ct = default)
    {
        if (timeZone is null && locale is null)
        {
            return Task.FromResult(ValidationError("Give timeZone, locale, or both."));
        }

        return Json(() => api.SendJsonAsync(HttpMethod.Patch, "/v1/tenant",
            Body(("timeZone", timeZone), ("locale", locale)), ct: ct));
    }

    [McpServerTool(Name = "list_adapters", ReadOnly = true)]
    [Description("See the kinds of live content source this workspace can create (RSS feed, weather, " +
                 "calendar and more), with the config fields each one needs. Use before create_source.")]
    public Task<CallToolResult> ListAdapters(CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, "/v1/adapters", ct: ct));

    [McpServerTool(Name = "create_source")]
    [Description("Create a content source InfoSlides fetches by itself, e.g. a news RSS feed for the " +
                 "ticker or a weather forecast for a slide. adapterType and the config fields come from " +
                 "list_adapters. For data your own system sends, use a push template instead " +
                 "(create_template with dataMode push, then add_dynamic_slide). Plans without live sources " +
                 "return EntitlementRequired with an upgrade link.")]
    public Task<CallToolResult> CreateSource(
        [Description("Adapter type from list_adapters, e.g. 'RssFeed'.")] string adapterType,
        [Description("Display name, e.g. 'RÚV news'.")] string name,
        [Description("Config object as JSON, e.g. {\"feedUrl\":\"https://...\"}.")] JsonElement? config = null,
        [Description("Fetch interval in seconds (default 3600).")] int? fetchIntervalSeconds = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, "/v1/sources",
            Body(("adapterType", adapterType), ("name", name), ("config", Node(config)), ("fetchIntervalSeconds", fetchIntervalSeconds)),
            idempotent: true, ct));

    [McpServerTool(Name = "update_source_settings")]
    [Description("Change a content source's name, config, fetch interval, or pause and resume it (ids " +
                 "from list_sources). To send data to a push source, use push_data instead.")]
    public Task<CallToolResult> EditSource(
        [Description("Id of the source.")] string sourceId,
        [Description("New display name.")] string? name = null,
        [Description("New config object as JSON.")] JsonElement? config = null,
        [Description("New fetch interval in seconds.")] int? fetchIntervalSeconds = null,
        [Description("False pauses fetching, true resumes it.")] bool? isEnabled = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Patch, $"/v1/sources/{Id(sourceId)}",
            Body(("name", name), ("config", Node(config)), ("fetchIntervalSeconds", fetchIntervalSeconds), ("isEnabled", isEnabled)),
            ct: ct));

    [McpServerTool(Name = "delete_source", Destructive = true)]
    [Description("Delete a content source. Slides and tickers fed by it stop updating. Confirm with the " +
                 "person first.")]
    public Task<CallToolResult> DeleteSource(
        [Description("Id of the source.")] string sourceId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Delete, $"/v1/sources/{Id(sourceId)}", ct: ct));

    [McpServerTool(Name = "get_workspace_health", ReadOnly = true)]
    [Description("\"How are my screens?\" in one call: a sentence to read out (\"11 of 12 screens are online; " +
                 "Lobby has been offline since 09:12.\"), every screen with online and lastSeenAt, usage " +
                 "against the plan, and problems most urgent first (offline screens, failed renders, " +
                 "slideshows no screen plays, expired plan, limits near full), each with a code, a message " +
                 "and a ready-made fix. Empty problems means all is well.")]
    public Task<CallToolResult> GetWorkspaceHealth(CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, "/v1/workspace/health", ct: ct));

    [McpServerTool(Name = "get_offline_alerts", ReadOnly = true)]
    [Description("\"When do I get emails about screens going offline?\" Shows the workspace's quiet hours " +
                 "and, per screen, whether alerts are on, its own quiet hours and whether it follows its " +
                 "schedule. At most one alert per screen per day is sent.")]
    public Task<CallToolResult> GetOfflineAlerts(CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, "/v1/workspace/offline-alerts", ct: ct));

    [McpServerTool(Name = "set_workspace_offline_alerts")]
    [Description("\"Don't email me about screens at night.\" Sets the quiet hours for offline alert emails " +
                 "(alerts wait until they end). Give quietStart and quietEnd; give neither to alert at any " +
                 "hour. Screens with their own quiet hours keep them. Workspace admins only.")]
    public Task<CallToolResult> SetWorkspaceOfflineAlerts(
        [Description("Quiet hours start, HH:mm in the workspace time zone, e.g. '22:00'.")] string? quietStart = null,
        [Description("Quiet hours end, HH:mm; before quietStart spans midnight, e.g. '07:00'.")] string? quietEnd = null,
        CancellationToken ct = default)
    {
        if ((quietStart is null) != (quietEnd is null))
        {
            return Task.FromResult(ValidationError("Give quietStart and quietEnd together, or neither."));
        }

        return Json(() => api.SendJsonAsync(HttpMethod.Put, "/v1/workspace/offline-alerts",
            new JsonObject { ["quietHours"] = ScreenTools.Quiet(quietStart, quietEnd) }, ct: ct));
    }

    [McpServerTool(Name = "undo_change", Destructive = true)]
    [Description("Undo the last change: puts back what an earlier call changed. Calls that change " +
                 "something return an undo field; pass its body.token here within 24 hours. A deleted " +
                 "slide comes back, an added slide, takeover or paired screen is removed. Not covered: " +
                 "deleting a slideshow, and screen settings. If the same thing was changed again since, " +
                 "the answer is NeedsClarification: ask the person before sending force=true, which " +
                 "overwrites that later change.")]
    public Task<CallToolResult> UndoChange(
        [Description("The token from the undo field of the earlier result.")] string token,
        [Description("True to overwrite a later change; only after the person agreed.")] bool force = false,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, "/v1/undo",
            Body(("token", token), ("force", force ? true : null)), ct: ct));

    [McpServerTool(Name = "report_issue")]
    [Description("Tell the InfoSlides team you could not do what the person asked because of InfoSlides " +
                 "itself: no tool for it, an error you could not get past, or docs that led you wrong. The " +
                 "team reads every report. Leave out personal data. Then tell the person plainly what you " +
                 "could not do; do not retry the same failing call in a loop. At most 50 reports and " +
                 "testimonials per workspace a day.")]
    public Task<CallToolResult> ReportIssue(
        [Description("What went wrong, or what you could not do.")] string problem,
        [Description("What the person asked for, in their words.")] string? intent = null,
        [Description("The call that failed, e.g. 'PATCH /v1/slides/{id}' or a tool name.")] string? request = null,
        [Description("Your product name, e.g. 'Claude'.")] string? agent = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, "/v1/feedback/issues",
            Body(("problem", problem), ("intent", intent), ("request", request), ("agent", agent)), ct: ct));

    [McpServerTool(Name = "leave_testimonial")]
    [Description("After a job went well, give your own verdict on working with InfoSlides. It may be shown " +
                 "on the InfoSlides website as a quote attributed to your product name, as an AI agent. " +
                 "Write it in your own words; never quote the person, and leave out personal data.")]
    public Task<CallToolResult> LeaveTestimonial(
        [Description("Your verdict, as you would want it shown, e.g. 'Had the lunch menu on the lobby TV in two calls.'")] string quote,
        [Description("What you did that the verdict is about, e.g. 'set up a live menu board'.")] string? context = null,
        [Description("Your product name, e.g. 'Claude'.")] string? agent = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, "/v1/feedback/testimonials",
            Body(("quote", quote), ("context", context), ("agent", agent)), ct: ct));

    [McpServerTool(Name = "make_ai_slide")]
    [Description("AI Studio: design a slide in the workspace's style from a description (handler prompt: " +
                 "headline, details, prices, times), a photo of a poster, menu or whiteboard (photo: " +
                 "mediaAssetId from upload_media, or mediaUrl), a web page (url) or a PDF/Word file " +
                 "(document: mediaAssetId). Returns a job at once: poll get_ai_slide_job every 5 seconds " +
                 "(10 to 60 seconds). By default it stops at Ready with preview images: show them to the " +
                 "person, then insert_ai_slides. To show a photo as it is, use add_media_slide instead. " +
                 "EntitlementRequired comes with an upgrade link; trialAvailable in its details means " +
                 "start_ai_studio_trial is possible: ask the person first.")]
    public Task<CallToolResult> MakeAiSlide(
        [Description("The slideshow to add the slides to.")] string slideshowId,
        [Description("prompt, photo, url or document.")] string handler,
        [Description("What the slide should say (handler prompt).")] string? prompt = null,
        [Description("Page address (handler url).")] string? url = null,
        [Description("Uploaded media id (photo, document, or a background image for prompt).")] string? mediaAssetId = null,
        [Description("Public address of a photo.")] string? mediaUrl = null,
        [Description("True to add the slides straight away without a preview step.")] bool insert = false,
        [Description("True to feature logo and brand colour, false to keep branding subtle; omit to let the prompt decide.")] bool? branding = null,
        [Description("auto, none (plain colour) or photo (stock photo).")] string? background = null,
        [Description("With background photo: what the photo should show.")] string? backgroundDescription = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, $"/v1/slideshows/{Id(slideshowId)}/slides/ai",
            Body(("handler", handler), ("prompt", prompt), ("url", url), ("mediaAssetId", mediaAssetId), ("mediaUrl", mediaUrl),
                ("insert", insert ? true : null), ("branding", branding), ("background", background),
                ("backgroundDescription", backgroundDescription)),
            idempotent: true, ct));

    [McpServerTool(Name = "get_ai_slide_job", ReadOnly = true)]
    [Description("Check an AI-designed slide job: Processing, Ready (previews to show the person), Inserted, " +
                 "NeedsClarification (read its question out, then start a new job with the answer in the " +
                 "prompt) or Failed. Jobs are kept for 24 hours. Poll at most every 5 seconds.")]
    public Task<CallToolResult> GetAiSlideJob(
        [Description("The slideshow the job belongs to.")] string slideshowId,
        [Description("Id of the job from make_ai_slide.")] string jobId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, $"/v1/slideshows/{Id(slideshowId)}/slides/ai/{Id(jobId)}", ct: ct));

    [McpServerTool(Name = "insert_ai_slides")]
    [Description("Add the previews of a Ready AI slide job to the slideshow, after the person approved " +
                 "them. Safe to repeat: it does not add them twice. Queues a re-render.")]
    public Task<CallToolResult> InsertAiSlides(
        [Description("The slideshow the job belongs to.")] string slideshowId,
        [Description("Id of the Ready job.")] string jobId,
        [Description("Zero-based position; appended when omitted.")] int? position = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, $"/v1/slideshows/{Id(slideshowId)}/slides/ai/{Id(jobId)}/insert",
            Body(("position", position)), idempotent: true, ct));

    [McpServerTool(Name = "start_ai_studio_trial")]
    [Description("Start the workspace's free AI Studio trial (7 days or 3 slides, prompt handler only). " +
                 "Once per workspace, so ask the person first. Calling it again while it runs returns the " +
                 "same trial.")]
    public Task<CallToolResult> StartAiStudioTrial(CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, "/v1/ai-studio/trial", idempotent: true, ct: ct));

    [McpServerTool(Name = "preview_new_template", ReadOnly = true)]
    [Description("See what a template design looks like as an image before saving it with create_template. " +
                 "Give the layout HTML and CSS and sample data; nothing is saved. Rate limited.")]
    public Task<CallToolResult> PreviewTemplate(
        [Description("Template HTML; every {{field}} is a data field.")] string layoutHtml,
        [Description("Template CSS.")] string? layoutCss = null,
        [Description("Example data as a JSON object.")] JsonElement? sampleData = null,
        [Description("16:9 (default), 9:16 or 1:1.")] string? aspectRatio = null,
        CancellationToken ct = default) =>
        ExecutePng(() => api.GetPngAsync(HttpMethod.Post, "/v1/templates/preview",
                Body(("layoutHtml", layoutHtml), ("layoutCss", layoutCss), ("sampleData", Node(sampleData)), ("aspectRatio", aspectRatio)), ct),
            "Template preview");

    private static JsonNode? Node(JsonElement? value) =>
        value is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } v ? JsonNode.Parse(v.GetRawText()) : null;
}
