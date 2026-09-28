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

    [McpServerTool(Name = "revoke_invitation", Destructive = true)]
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

    [McpServerTool(Name = "update_workspace_settings")]
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

    [McpServerTool(Name = "edit_source")]
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

    private static JsonNode? Node(JsonElement? value) =>
        value is { ValueKind: not (JsonValueKind.Undefined or JsonValueKind.Null) } v ? JsonNode.Parse(v.GetRawText()) : null;
}
