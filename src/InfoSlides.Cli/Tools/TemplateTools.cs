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
                 "'css'. The example data defines the shape every later update_source push must " +
                 "match. Set dryRun=true to check it without creating anything.")]
    public Task<CallToolResult> CreateTemplate(
        [Description("What this layout is for, e.g. 'Soup of the day' or 'Live sales board'.")] string title,
        [Description("Description of how the slide should look (AI mode). Requires sampleJson.")] string? prompt = null,
        [Description("Example of the data this slide will show, as JSON — defines the shape update_source must send.")] JsonElement? sampleJson = null,
        [Description("Finished HTML with {{field}} placeholders (code mode).")] string? html = null,
        [Description("Optional stylesheet for the custom HTML (code mode).")] string? css = null,
        [Description("Check the layout without creating it.")] bool dryRun = false,
        CancellationToken ct = default)
    {
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
            () => api.CreateTemplateAsync(new CreateTemplateRequest(title, prompt, sampleJson, html, css), dryRun, ct),
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
                 "(see list_templates). This is also the only tool a restricted push-only " +
                 "(isk_dp_) key can call, which is how an external system safely feeds one slide. Set " +
                 "dryRun=true to check the data without changing what is on screen.")]
    public Task<CallToolResult> UpdateSource(
        [Description("Id of the slide to update.")] string slideId,
        [Description("The new values, as a JSON object matching the template's example data.")] JsonElement data,
        [Description("Check the data without changing what is on screen.")] bool dryRun = false,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.UpdateSourceAsync(slideId, data, dryRun, ct),
            InfoSlidesJsonContext.Default.OkResult);
}
