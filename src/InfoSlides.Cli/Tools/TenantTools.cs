using System.ComponentModel;
using InfoSlides.Core.Api;
using InfoSlides.Core.Models;
using InfoSlides.Core.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InfoSlides.Cli.Tools;

[McpServerToolType]
public sealed class TenantTools(InfoSlidesApiClient api)
{
    [McpServerTool(Name = "create_tenant", Title = "Create workspace", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Start here when someone wants content on a TV or screen but has no InfoSlides " +
                 "account yet — a café putting its menu on a screen, a hotel with a lobby display, a " +
                 "school noticeboard, a shop window. Sets up their workspace and returns the Primary " +
                 "Admin API Key used by every later call. This is the only tool that needs no " +
                 "credentials. The new account lands on the permanent free plan: 1 screen, 4 " +
                 "slideshows, no credit card, nothing expires. Before creating it, give the person the " +
                 "Terms of Service (https://infoslides.app/terms) and Privacy Policy " +
                 "(https://infoslides.app/privacy) links. ownerEmail must be an address you can open right " +
                 "now, never an invented one: your own is recommended, so you can verify it at once and " +
                 "then add the person with invite_team_member. Adding a screen fails with EmailNotVerified " +
                 "until the address is verified.")]
    public Task<CallToolResult> CreateTenant(
        [Description("Name of the workspace — usually the company, venue, or shop name, e.g. 'Acme Cafe'.")] string tenantName,
        [Description("Email address of the owner. Receives the verification email and the sign-in details.")] string ownerEmail,
        [Description("Optional IANA time zone, e.g. 'Europe/London'; decides when schedules switch and what clocks show.")] string? timeZone = null,
        [Description("Optional locale for number and date formats, e.g. 'en-GB'.")] string? locale = null,
        CancellationToken ct = default) =>
        // Source is fixed to "mcp" rather than exposed as a parameter: it records how the account was
        // provisioned (InfoSlides story AGENT-01), and letting a model choose it would corrupt the
        // one signal that makes agent-originated signups countable.
        ToolResults.Execute(() => api.CreateTenantAsync(new CreateTenantRequest(tenantName, ownerEmail, "mcp", timeZone, locale), ct),
            InfoSlidesJsonContext.Default.CreateTenantResult);

    [McpServerTool(Name = "get_tenant_info", Title = "Get workspace info", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Check what this account is allowed to do before planning any screen work: workspace " +
                 "name, owner, whether their email is confirmed, which plan they are on, how many " +
                 "screens are in use out of the allowance, and the scope of the API key in use. Call " +
                 "this early — designing around the limits beats discovering them through errors " +
                 "halfway through a setup.")]
    public Task<CallToolResult> GetTenantInfo(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.GetTenantInfoAsync(ct), InfoSlidesJsonContext.Default.TenantInfo);

    [McpServerTool(Name = "resend_verification_email", Title = "Resend verification email", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Send the owner's confirmation email again. Use this when another tool fails with " +
                 "EmailNotVerified — the owner has to click the link in that email before screens can " +
                 "be registered. Tell the user to check their spam folder.")]
    public Task<CallToolResult> ResendVerificationEmail(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ResendVerificationEmailAsync(ct), InfoSlidesJsonContext.Default.OkResult);
}
