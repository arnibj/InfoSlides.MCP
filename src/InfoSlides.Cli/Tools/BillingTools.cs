using System.ComponentModel;
using InfoSlides.Core.Api;
using InfoSlides.Core.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InfoSlides.Cli.Tools;

[McpServerToolType]
public sealed class BillingTools(InfoSlidesApiClient api)
{
    [McpServerTool(Name = "upgrade_subscription", ReadOnly = true)]
    [Description("Get a Paddle checkout link to upgrade the tenant to Premium (unlimited devices, AI " +
                 "template generation, update_source). Hand the link to the user; entitlements unlock " +
                 "automatically once payment is confirmed via webhook.")]
    public Task<CallToolResult> UpgradeSubscription(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.CreateCheckoutAsync(ct), InfoSlidesJsonContext.Default.CheckoutLink);
}
