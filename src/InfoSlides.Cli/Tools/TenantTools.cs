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
    [McpServerTool(Name = "create_tenant", Idempotent = true)]
    [Description("Start here when someone wants content on a TV or screen but has no InfoSlides " +
                 "account yet — a café putting its menu on a screen, a hotel with a lobby display, a " +
                 "school noticeboard, a shop window. Sets up their workspace and returns the Primary " +
                 "Admin API Key used by every later call. This is the only tool that needs no " +
                 "credentials. The new account lands on the permanent free plan: 1 screen, 4 " +
                 "slideshows, no credit card, nothing expires. A verification email goes to the owner " +
                 "and a few tools stay locked until they click it.")]
    public Task<CallToolResult> CreateTenant(
        [Description("Name of the workspace — usually the company, venue, or shop name, e.g. 'Acme Cafe'.")] string tenantName,
        [Description("Email address of the owner. Receives the verification email and the sign-in details.")] string ownerEmail,
        CancellationToken ct = default) =>
        // Source is fixed to "mcp" rather than exposed as a parameter: it records how the account was
        // provisioned (InfoSlides story AGENT-01), and letting a model choose it would corrupt the
        // one signal that makes agent-originated signups countable.
        ToolResults.Execute(() => api.CreateTenantAsync(new CreateTenantRequest(tenantName, ownerEmail, "mcp"), ct),
            InfoSlidesJsonContext.Default.CreateTenantResult);

    [McpServerTool(Name = "get_tenant_info", ReadOnly = true)]
    [Description("Check what this account is allowed to do before planning any screen work: workspace " +
                 "name, owner, whether their email is confirmed, which plan they are on, how many " +
                 "screens are in use out of the allowance, and the scope of the API key in use. Call " +
                 "this early — designing around the limits beats discovering them through errors " +
                 "halfway through a setup.")]
    public Task<CallToolResult> GetTenantInfo(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.GetTenantInfoAsync(ct), InfoSlidesJsonContext.Default.TenantInfo);

    [McpServerTool(Name = "resend_verification_email")]
    [Description("Send the owner's confirmation email again. Use this when another tool fails with " +
                 "EmailNotVerified — the owner has to click the link in that email before screens can " +
                 "be registered. Tell the user to check their spam folder.")]
    public Task<CallToolResult> ResendVerificationEmail(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ResendVerificationEmailAsync(ct), InfoSlidesJsonContext.Default.OkResult);
}
