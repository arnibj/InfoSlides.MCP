using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using InfoSlides.Core.Api;
using InfoSlides.Core.Models;
using InfoSlides.Core.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InfoSlides.Mcp.Tools;

[McpServerToolType]
public sealed class TemplateTools(InfoSlidesApiClient api)
{
    [McpServerTool(Name = "create_template", Title = "Create slide template", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = false)]
    [Description("Design a screen layout that fills itself in from live data, so the display stays " +
                 "current without anyone editing it — today's soup and price, the current exchange " +
                 "rate, a live sales counter, the next departure time. Requires a paid plan. Two ways " +
                 "to build it: describe the look in 'prompt' and give an example of the data in " +
                 "'sampleJson', or hand over finished 'html' with {{field}} placeholders plus optional " +
                 "'css' (leave sampleJson out: the placeholders are the fields). That shape is what " +
                 "every later push must match. Set " +
                 "dataMode='push' when the data will come from the user's own system (a queue, meters, " +
                 "scores): the slide then gets a push source, stays hidden until the first data " +
                 "arrives, and every plan includes one such live data slide, the free plan too (code " +
                 "mode). Design rules and a worked example: " +
                 "https://infoslides.app/blog/agents-guide-to-the-infoslides-galaxy. Set dryRun=true " +
                 "to check it without creating anything.")]
    public Task<CallToolResult> CreateTemplate(
        [Description("What this layout is for, e.g. 'Soup of the day' or 'Live sales board'.")] string title,
        [Description("Description of how the slide should look (AI mode). Requires sampleJson.")] string? prompt = null,
        [Description("Example of the data this slide will show, as JSON (AI mode only, with prompt). Defines the shape update_source must send.")] JsonObject? sampleJson = null,
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
            () => api.CreateTemplateAsync(new CreateTemplateRequest(title, prompt, ToolResults.ToElement(sampleJson), html, html is null ? css : css ?? "", dataMode?.ToLowerInvariant()), dryRun, ct),
            InfoSlidesJsonContext.Default.Template);
    }

    [McpServerTool(Name = "list_templates", Title = "List slide templates", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("See the self-updating screen layouts available to this workspace, each with an " +
                 "example of the data it expects. Check that example before pushing values with " +
                 "update_source, and use this to find a ready-made layout instead of building one.")]
    public Task<CallToolResult> ListTemplates(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ListTemplatesAsync(ct), InfoSlidesJsonContext.Default.ListTemplate);

    private static string Id(string id) => Uri.EscapeDataString(id);

    [McpServerTool(Name = "list_template_categories", Title = "List template categories", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("List the template category names this workspace may use, such as News or Generic: the same set " +
                 "that decides which built-in layouts list_templates shows.")]
    public Task<CallToolResult> ListTemplateCategories(CancellationToken ct = default) =>
        ToolResults.Json(() => api.SendJsonAsync(HttpMethod.Get, "/v1/templates/categories", ct: ct));

    [McpServerTool(Name = "get_template", Title = "Get slide template", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Read one self-updating layout: title, sampleJson (its data fields with example values, the shape " +
                 "a push to its source must send), html and css. Read it before changing it with update_template.")]
    public Task<CallToolResult> GetTemplate(
        [Description("Id of the template.")] string templateId,
        CancellationToken ct = default) =>
        ToolResults.Json(() => api.SendJsonAsync(HttpMethod.Get, $"/v1/templates/{Id(templateId)}", ct: ct));

    [McpServerTool(Name = "update_template", Title = "Update slide template", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Change a layout the workspace owns: title, html, css and/or schema (the data fields, e.g. " +
                 "{\"fields\":[{\"name\":\"sales\",\"type\":\"number\"}]}). Send only what should change; new html " +
                 "without a schema takes its fields from the {{field}} tokens. Slides already using it pick up the " +
                 "change, so check get_template_usage first. A built-in layout cannot be changed: copy it with " +
                 "clone_template and change the copy.")]
    public Task<CallToolResult> UpdateTemplate(
        [Description("Id of the template.")] string templateId,
        [Description("New title.")] string? title = null,
        [Description("New HTML with {{field}} placeholders.")] string? html = null,
        [Description("New stylesheet.")] string? css = null,
        [Description("The data fields as JSON, e.g. {\"fields\":[{\"name\":\"sales\",\"type\":\"number\"}]}.")] JsonObject? schema = null,
        CancellationToken ct = default)
    {
        if (title is null && html is null && css is null && schema is null)
        {
            return Task.FromResult(ToolResults.ValidationError("Give at least one of title, html, css or schema."));
        }

        return ToolResults.Json(() => api.SendJsonAsync(HttpMethod.Patch, $"/v1/templates/{Id(templateId)}",
            ToolResults.Body(("title", title), ("html", html), ("css", css), ("schema", schema)), ct: ct));
    }

    [McpServerTool(Name = "delete_template", Title = "Delete slide template", ReadOnly = false, Destructive = true, OpenWorld = false, Idempotent = true)]
    [Description("Delete a layout the workspace owns, for good. Confirm with the person first. Refused while a " +
                 "slide uses it: the error lists those slides, so change or remove them first. Built-in layouts " +
                 "cannot be deleted.")]
    public Task<CallToolResult> DeleteTemplate(
        [Description("Id of the template.")] string templateId,
        CancellationToken ct = default) =>
        ToolResults.Json(() => api.SendJsonAsync(HttpMethod.Delete, $"/v1/templates/{Id(templateId)}", ct: ct));

    [McpServerTool(Name = "clone_template", Title = "Copy slide template", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = false)]
    [Description("Copy a layout, usually a built-in one from list_templates, into the workspace as '<title> (Copy)', " +
                 "so it can be changed with update_template. Needs a plan with live data slides; the error carries " +
                 "an upgrade link otherwise.")]
    public Task<CallToolResult> CloneTemplate(
        [Description("Id of the template to copy.")] string templateId,
        CancellationToken ct = default) =>
        ToolResults.Json(() => api.SendJsonAsync(HttpMethod.Post, $"/v1/templates/{Id(templateId)}/clone", idempotent: true, ct: ct));

    [McpServerTool(Name = "get_template_usage", Title = "Get slide template usage", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Where a layout is used in this workspace: slideCount, slideshowCount and the slides (slideId, " +
                 "slideshowId, slideshowTitle). Check it before deleting or reworking a layout.")]
    public Task<CallToolResult> GetTemplateUsage(
        [Description("Id of the template.")] string templateId,
        CancellationToken ct = default) =>
        ToolResults.Json(() => api.SendJsonAsync(HttpMethod.Get, $"/v1/templates/{Id(templateId)}/usage", ct: ct));

    [McpServerTool(Name = "regenerate_template_thumbnail", Title = "Redraw template thumbnail", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Redraw the gallery picture of a layout the workspace owns, for example after its look changed. " +
                 "Returns thumbnailPath. Rate limited.")]
    public Task<CallToolResult> RegenerateTemplateThumbnail(
        [Description("Id of the template.")] string templateId,
        CancellationToken ct = default) =>
        ToolResults.Json(() => api.SendJsonAsync(HttpMethod.Post, $"/v1/templates/{Id(templateId)}/thumbnail", ct: ct));

    [McpServerTool(Name = "update_source", Title = "Update live slide data", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Put fresh information on the screen: send today's menu, the new price, the current " +
                 "total, the updated opening hours. The display re-renders itself server-side — nobody " +
                 "has to touch the TV. The data must match the shape the template's example defines " +
                 "(see list_templates). For a slide on a push source the data goes to that source, " +
                 "exactly as push_data would send it. A restricted push-only (isk_dp_) key bound to " +
                 "the slide can call this, which is how an external system safely feeds one slide. Set " +
                 "dryRun=true to check the data without changing what is on screen.")]
    public Task<CallToolResult> UpdateSource(
        [Description("Id of the slide to update.")] string slideId,
        [Description("The new values, as a JSON object matching the template's example data.")] JsonObject data,
        [Description("Check the data without changing what is on screen.")] bool dryRun = false,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.UpdateSourceAsync(slideId, ToolResults.ToElement(data)!.Value, dryRun, ct),
            InfoSlidesJsonContext.Default.OkResult);

    [McpServerTool(Name = "push_data", Title = "Push live data", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = false)]
    [Description("Send new data to a push source: the whole set of values the slide shows, as one " +
                 "JSON object with the template's field names. Every slide on the source updates, and a " +
                 "slide that was hidden waiting for data appears. Returns receivedAt. A push-only " +
                 "(isk_dp_) key bound to the source can call this, so hand that key to the system that " +
                 "sends the data. Set dryRun=true to check the data without storing it.")]
    public Task<CallToolResult> PushData(
        [Description("Id of the push source (the sourceId returned by add_dynamic_slide).")] string sourceId,
        [Description("The values, as a JSON object with the template's field names.")] JsonObject data,
        [Description("Check the data without storing it.")] bool dryRun = false,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.PushSourceDataAsync(sourceId, ToolResults.ToElement(data)!.Value, dryRun, ct),
            InfoSlidesJsonContext.Default.PushReceived);

    [McpServerTool(Name = "get_source_status", Title = "Get content source status", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Check a source. For a push source: when data last arrived (lastReceivedAt), whether " +
                 "its slides are showing data (isShowingData, false before the first push or after the " +
                 "data went stale), the staleness timeout, and which slides it feeds; use it to confirm a " +
                 "system is actually sending. For a fetched source (RSS, weather, etc.): lastFetchedAt " +
                 "and health (Healthy, Degraded or Failing) with lastError in plain words, consecutiveFailures " +
                 "and lastSuccessAt, with the push fields empty.")]
    public Task<CallToolResult> GetSourceStatus(
        [Description("Id of the source.")] string sourceId,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.GetSourceStatusAsync(sourceId, ct),
            InfoSlidesJsonContext.Default.PushSourceStatus);

    [McpServerTool(Name = "create_source_key", Title = "Create source API key", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = false)]
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
