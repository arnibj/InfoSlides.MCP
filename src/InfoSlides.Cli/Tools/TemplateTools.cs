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
    [Description("Create a dynamic slide template (Premium). Two modes: AI-prompt driven — pass 'prompt' " +
                 "plus 'sampleJson' describing the data schema; or code driven — pass raw 'html' (with " +
                 "{{field}} placeholders) and optional 'css'. Later update_source pushes must match the " +
                 "schema. Set dryRun=true to validate without creating.")]
    public Task<CallToolResult> CreateTemplate(
        [Description("Template title.")] string title,
        [Description("AI Studio visual prompt (AI mode). Requires sampleJson.")] string? prompt = null,
        [Description("Example JSON object defining the data schema (AI mode).")] JsonElement? sampleJson = null,
        [Description("Raw HTML with {{field}} placeholders (code mode).")] string? html = null,
        [Description("Optional stylesheet for the custom HTML (code mode).")] string? css = null,
        [Description("Validate the template without creating it.")] bool dryRun = false,
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
    [Description("List templates with their data schemas (sampleJson) — check the schema before update_source.")]
    public Task<CallToolResult> ListTemplates(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ListTemplatesAsync(ct), InfoSlidesJsonContext.Default.ListTemplate);

    [McpServerTool(Name = "update_source")]
    [Description("Push a JSON data payload to a template-based slide, triggering a server-side re-render " +
                 "(e.g. refresh a sales counter). The payload must match the template's schema. This is the " +
                 "only tool usable with a data-provider (isk_dp_) key. Set dryRun=true to validate the " +
                 "payload without rendering.")]
    public Task<CallToolResult> UpdateSource(
        [Description("Id of the slide to update.")] string slideId,
        [Description("JSON object matching the template's sampleJson schema.")] JsonElement data,
        [Description("Validate the payload without re-rendering.")] bool dryRun = false,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.UpdateSourceAsync(slideId, data, dryRun, ct),
            InfoSlidesJsonContext.Default.OkResult);
}
