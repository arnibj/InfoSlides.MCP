using System.ComponentModel;
using System.Text.Json;
using InfoSlides.Core.Api;
using InfoSlides.Core.Models;
using InfoSlides.Core.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InfoSlides.Cli.Tools;

[McpServerToolType]
public sealed class TemplateTools(InfoSlidesApiClient api)
{
    [McpServerTool(Name = "create_template")]
    [Description("Design a screen layout that fills itself in from live data, so the display stays " +
                 "current without anyone editing it — today's soup and price, the current exchange " +
                 "rate, a live sales counter, the next departure time. Requires a paid plan. Two ways " +
                 "to build it: describe the look in 'prompt' and give an example of the data in " +
                 "'sampleJson', or hand over finished 'html' with {{field}} placeholders plus optional " +
                 "'css'. The example data defines the shape every later push must match. Set " +
                 "dataMode='push' when the data will come from the user's own system (a queue, meters, " +
                 "scores): the slide then gets a push source, stays hidden until the first data " +
                 "arrives, and every plan includes one such live data slide, the free plan too (code " +
                 "mode). Design rules and a worked example: " +
                 "https://infoslides.app/blog/agents-guide-to-the-infoslides-galaxy. Set dryRun=true " +
                 "to check it without creating anything.")]
    public Task<CallToolResult> CreateTemplate(
        [Description("What this layout is for, e.g. 'Soup of the day' or 'Live sales board'.")] string title,
        [Description("Description of how the slide should look (AI mode). Requires sampleJson.")] string? prompt = null,
        [Description("Example of the data this slide will show, as JSON — defines the shape update_source must send.")] JsonElement? sampleJson = null,
        [Description("Finished HTML with {{field}} placeholders (code mode).")] string? html = null,
        [Description("Optional stylesheet for the custom HTML (code mode).")] string? css = null,
        [Description("'push' when an outside system will send the data (gives the slide a push source); omit otherwise.")] string? dataMode = null,
        [Description("Check the layout without creating it.")] bool dryRun = false,
        CancellationToken ct = default)
    {
        if (dataMode is not null && !string.Equals(dataMode, "push", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ToolResults.ValidationError("'dataMode' must be \"push\" or omitted."));
        }

        if (prompt is null && html is null)
        {
            return Task.FromResult(ToolResults.ValidationError(
                "Provide either 'prompt' (+ sampleJson) for AI generation, or 'html' (+ optional css) for a code template."));
        }

        if (prompt is not null && html is not null)
        {
            return Task.FromResult(ToolResults.ValidationError(
                "Provide 'prompt' or 'html', not both — the modes are mutually exclusive."));
        }

        if (prompt is not null && sampleJson is null)
        {
            return Task.FromResult(ToolResults.ValidationError(
                "'sampleJson' is mandatory when 'prompt' is used, so the generated template has a data schema."));
        }

        return ToolResults.Execute(
            () => api.CreateTemplateAsync(new CreateTemplateRequest(title, prompt, sampleJson, html, css, dataMode?.ToLowerInvariant()), dryRun, ct),
            InfoSlidesJsonContext.Default.Template);
    }

    [McpServerTool(Name = "list_templates", ReadOnly = true)]
    [Description("See the self-updating screen layouts available to this workspace, each with an " +
                 "example of the data it expects. Check that example before pushing values with " +
                 "update_source, and use this to find a ready-made layout instead of building one.")]
    public Task<CallToolResult> ListTemplates(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ListTemplatesAsync(ct), InfoSlidesJsonContext.Default.ListTemplate);

    [McpServerTool(Name = "update_source")]
    [Description("Put fresh information on the screen: send today's menu, the new price, the current " +
                 "total, the updated opening hours. The display re-renders itself server-side — nobody " +
                 "has to touch the TV. The data must match the shape the template's example defines " +
                 "(see list_templates). For a slide on a push source the data goes to that source, " +
                 "exactly as push_data would send it. A restricted push-only (isk_dp_) key bound to " +
                 "the slide can call this, which is how an external system safely feeds one slide. Set " +
                 "dryRun=true to check the data without changing what is on screen.")]
    public Task<CallToolResult> UpdateSource(
        [Description("Id of the slide to update.")] string slideId,
        [Description("The new values, as a JSON object matching the template's example data.")] JsonElement data,
        [Description("Check the data without changing what is on screen.")] bool dryRun = false,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.UpdateSourceAsync(slideId, data, dryRun, ct),
            InfoSlidesJsonContext.Default.OkResult);

    [McpServerTool(Name = "push_data")]
    [Description("Send new data to a push source: the whole set of values the slide shows, as one " +
                 "JSON object with the template's field names. Every slide on the source updates, and a " +
                 "slide that was hidden waiting for data appears. Returns receivedAt. A push-only " +
                 "(isk_dp_) key bound to the source can call this, so hand that key to the system that " +
                 "sends the data. Set dryRun=true to check the data without storing it.")]
    public Task<CallToolResult> PushData(
        [Description("Id of the push source (the sourceId returned by add_dynamic_slide).")] string sourceId,
        [Description("The values, as a JSON object with the template's field names.")] JsonElement data,
        [Description("Check the data without storing it.")] bool dryRun = false,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.PushSourceDataAsync(sourceId, data, dryRun, ct),
            InfoSlidesJsonContext.Default.PushReceived);

    [McpServerTool(Name = "get_source_status", ReadOnly = true)]
    [Description("Check a push source: when data last arrived (lastReceivedAt), whether its slides " +
                 "are showing data (isShowingData, false before the first push or after the data " +
                 "went stale), the staleness timeout, and which slides it feeds. Use it to confirm a " +
                 "system is actually sending.")]
    public Task<CallToolResult> GetSourceStatus(
        [Description("Id of the push source.")] string sourceId,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.GetSourceStatusAsync(sourceId, ct),
            InfoSlidesJsonContext.Default.PushSourceStatus);

    [McpServerTool(Name = "create_source_key")]
    [Description("Create a push-only key for a push source, to give to the system that sends its " +
                 "data. The key can push to this one source and do nothing else. It is returned once: " +
                 "show it to the user or store it where the system can read it, because it cannot be " +
                 "shown again.")]
    public Task<CallToolResult> CreateSourceKey(
        [Description("Id of the push source.")] string sourceId,
        [Description("A label for the key, e.g. the system that will use it.")] string? name = null,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.CreateSourceKeyAsync(sourceId, name, ct),
            InfoSlidesJsonContext.Default.PushKey);
}
