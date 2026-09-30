using System.ComponentModel;
using InfoSlides.Core.Api;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static InfoSlides.Cli.Tools.ToolResults;

namespace InfoSlides.Cli.Tools;

[McpServerToolType]
public sealed class BillingTools(InfoSlidesApiClient api)
{
    [McpServerTool(Name = "upgrade_subscription", Title = "Upgrade subscription", ReadOnly = false, Destructive = false, OpenWorld = false)]
    [Description("Get the checkout link when the plan runs out: a second screen is needed, or the user " +
                 "wants self-updating slides fed by live data. Reach for this after a DeviceLimitReached " +
                 "or EntitlementRequired error rather than telling the user the thing cannot be done. Ask " +
                 "monthly or annual first and pass plan and billingPeriod: the result is then a Paddle " +
                 "checkout (/checkout?_ptxn=...) with its price, which needs no InfoSlides sign-in and pays " +
                 "for this workspace only. Without billingPeriod the link opens the plans page. Paying needs " +
                 "the person's clear yes to this plan at this price. Before they commit, tell them the price " +
                 "from the result, that it renews every month or year until cancelled, that they can cancel " +
                 "at any time and keep the plan until the period ends, and link " +
                 "https://infoslides.app/refund-policy. Then hand them the link, or open it and pay once they " +
                 "have said yes. A workspace that already pays gets its billing page, which needs a signed-in " +
                 "admin. The plan unlocks by itself once payment goes through.")]
    public Task<CallToolResult> UpgradeSubscription(
        [Description("Starter, Professional or Business.")] string? plan = null,
        [Description("monthly or annual.")] string? billingPeriod = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, "/v1/billing/checkout",
            Body(("plan", plan), ("billingPeriod", billingPeriod)), ct: ct));
}
