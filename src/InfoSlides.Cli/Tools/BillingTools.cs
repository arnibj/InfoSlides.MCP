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
    [Description("Get the checkout link when the free plan runs out — a second screen is needed, or " +
                 "the user wants self-updating slides fed by live data. Hand the link to the user to " +
                 "open; the extra capability unlocks by itself once payment goes through. Reach for " +
                 "this after a DeviceLimitReached or EntitlementRequired error rather than telling the " +
                 "user the thing cannot be done.")]
    public Task<CallToolResult> UpgradeSubscription(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.CreateCheckoutAsync(ct), InfoSlidesJsonContext.Default.CheckoutLink);
}
