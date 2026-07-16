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
    [Description("Create a new InfoSlides tenant (workspace). This is the only anonymous call: " +
                 "it returns the Primary Admin API Key used for every subsequent request. " +
                 "A verification email is sent to the owner; most tools stay locked until it is confirmed.")]
    public Task<CallToolResult> CreateTenant(
        [Description("Name of the tenant/workspace, e.g. the company or venue name.")] string tenantName,
        [Description("Email address of the tenant owner. Receives the verification email.")] string ownerEmail,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.CreateTenantAsync(new CreateTenantRequest(tenantName, ownerEmail), ct),
            InfoSlidesJsonContext.Default.CreateTenantResult);

    [McpServerTool(Name = "get_tenant_info", ReadOnly = true)]
    [Description("Get the current tenant: name, owner, email verification state, subscription level, " +
                 "device quota (used/max) and the scope of the API key in use. Call this first to plan " +
                 "around entitlements instead of discovering them through errors.")]
    public Task<CallToolResult> GetTenantInfo(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.GetTenantInfoAsync(ct), InfoSlidesJsonContext.Default.TenantInfo);

    [McpServerTool(Name = "resend_verification_email")]
    [Description("Re-send the owner verification email. Use this when a call fails with EmailNotVerified.")]
    public Task<CallToolResult> ResendVerificationEmail(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ResendVerificationEmailAsync(ct), InfoSlidesJsonContext.Default.OkResult);
}
