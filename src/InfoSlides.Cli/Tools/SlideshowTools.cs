using System.ComponentModel;
using InfoSlides.Core.Api;
using InfoSlides.Core.Models;
using InfoSlides.Core.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InfoSlides.Cli.Tools;

[McpServerToolType]
public sealed class SlideshowTools(InfoSlidesApiClient api)
{
    [McpServerTool(Name = "upload_slideshow")]
    [Description("Create the content that will play on a screen — the lunch menu, this week's offers, " +
                 "opening hours, staff notices, a welcome message for a hotel lobby. A slideshow is a " +
                 "deck of slides that loops on the display. Resolution defaults to 1920x1080 for a " +
                 "normal wall-mounted TV; use 1080x1920 for a portrait screen standing on its end, " +
                 "which is common for menu boards and shop windows. Slides can be added inline here or " +
                 "later with add_media_slide.")]
    public Task<CallToolResult> UploadSlideshow(
        [Description("What this content is, e.g. 'Lunch menu' or 'Reception welcome screen'.")] string title,
        [Description("Screen width in pixels (default 1920 — landscape TV).")] int width = 1920,
        [Description("Screen height in pixels (default 1080 — landscape TV).")] int height = 1080,
        [Description("Optional initial slides (mediaUrl and/or templateId, optional durationSeconds).")]
        List<NewSlide>? slides = null,
        CancellationToken ct = default) =>
        ToolResults.Execute(
            () => api.CreateSlideshowAsync(new CreateSlideshowRequest(title, new Resolution(width, height), slides), ct),
            InfoSlidesJsonContext.Default.Slideshow);

    [McpServerTool(Name = "update_slideshow")]
    [Description("Change what is already playing on a screen: rename it, switch it between landscape " +
                 "and portrait, or reorder the slides. Pass slideOrder as the complete list of slide " +
                 "ids in the sequence you want them shown.")]
    public Task<CallToolResult> UpdateSlideshow(
        [Description("Id of the slideshow to update.")] string slideshowId,
        [Description("New title, if changing.")] string? title = null,
        [Description("New width in pixels, if changing (requires height too).")] int? width = null,
        [Description("New height in pixels, if changing (requires width too).")] int? height = null,
        [Description("Complete ordered list of slide ids, if reordering.")] List<string>? slideOrder = null,
        CancellationToken ct = default)
    {
        if (width.HasValue != height.HasValue)
        {
            return Task.FromResult(ToolResults.ValidationError("Provide width and height together."));
        }

        var resolution = width.HasValue ? new Resolution(width.Value, height!.Value) : null;
        return ToolResults.Execute(
            () => api.UpdateSlideshowAsync(slideshowId, new UpdateSlideshowRequest(title, resolution, slideOrder), ct),
            InfoSlidesJsonContext.Default.Slideshow);
    }

    [McpServerTool(Name = "list_slideshows", ReadOnly = true)]
    [Description("See everything this workspace can put on a screen, with the shape (landscape or " +
                 "portrait) each one is built for. Use it to find the id of existing content before " +
                 "editing it or assigning it to a display.")]
    public Task<CallToolResult> ListSlideshows(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ListSlideshowsAsync(ct), InfoSlidesJsonContext.Default.ListSlideshow);

    [McpServerTool(Name = "get_slideshow", ReadOnly = true)]
    [Description("Look inside one piece of screen content: every slide, the order they play in, how " +
                 "long each is shown, and any rules about when they appear. Use this before editing so " +
                 "you know what is already there.")]
    public Task<CallToolResult> GetSlideshow(
        [Description("Id of the slideshow.")] string slideshowId,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.GetSlideshowAsync(slideshowId, ct), InfoSlidesJsonContext.Default.Slideshow);

    [McpServerTool(Name = "clone_slideshow")]
    [Description("Copy an existing deck so it can be customised without touching the original — or, " +
                 "with fromGallery=true, start from a ready-made professional design instead of a " +
                 "blank screen. Cloning from the gallery is the fastest way to get something " +
                 "presentable on a TV; call list_gallery first to see what is available.")]
    public Task<CallToolResult> CloneSlideshow(
        [Description("Id of the source slideshow, or gallery item id when fromGallery=true.")] string sourceId,
        [Description("Clone a ready-made gallery design instead of the workspace's own slideshow.")] bool fromGallery = false,
        CancellationToken ct = default) =>
        ToolResults.Execute(
            () => fromGallery ? api.CloneGalleryItemAsync(sourceId, ct) : api.CloneSlideshowAsync(sourceId, ct),
            InfoSlidesJsonContext.Default.Slideshow);

    [McpServerTool(Name = "list_gallery", ReadOnly = true)]
    [Description("Browse ready-made screen designs — menu boards, welcome screens, notice layouts — " +
                 "that can be copied into the workspace with clone_slideshow(fromGallery=true). Start " +
                 "here when the user wants something on a screen quickly and has no content prepared.")]
    public Task<CallToolResult> ListGallery(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ListGalleryAsync(ct), InfoSlidesJsonContext.Default.ListGalleryItem);

    [McpServerTool(Name = "add_media_slide")]
    [Description("Put a picture or a video on the screen — a photo of the specials board, a poster, a " +
                 "promo clip, a logo. Takes either a publicly reachable URL (downloaded server-side) or " +
                 "the id of a file already in the media library (see upload_media); provide exactly " +
                 "one. If the picture's shape does not match the screen's, the call still succeeds but " +
                 "returns an AspectMismatch warning — fix it rather than letting content be stretched " +
                 "or cropped on a display the public can see.")]
    public Task<CallToolResult> AddMediaSlide(
        [Description("Id of the slideshow to add the slide to.")] string slideshowId,
        [Description("Publicly reachable URL of the image/video; omit when using mediaAssetId.")] string? mediaUrl = null,
        [Description("Id of a file already in the media library (e.g. from upload_media); omit when using mediaUrl.")] string? mediaAssetId = null,
        [Description("How long the slide stays on screen, in seconds.")] double? durationSeconds = null,
        [Description("Zero-based position in the loop; appended when omitted.")] int? position = null,
        CancellationToken ct = default)
    {
        if ((mediaUrl is null) == (mediaAssetId is null))
        {
            return Task.FromResult(ToolResults.ValidationError("Provide exactly one of mediaUrl or mediaAssetId."));
        }

        return ToolResults.Execute(
            () => api.AddMediaSlideAsync(
                slideshowId, new AddMediaSlideRequest(mediaUrl, mediaAssetId, durationSeconds, position), ct),
            InfoSlidesJsonContext.Default.Slide);
    }

    [McpServerTool(Name = "add_dynamic_slide")]
    [Description("Add a slide that keeps itself up to date instead of showing a fixed picture — " +
                 "today's soup, the current queue number, live sales figures, tomorrow's weather. " +
                 "Needs a template first (see create_template or list_templates). The slide starts " +
                 "empty; push the actual values in with update_source.")]
    public Task<CallToolResult> AddDynamicSlide(
        [Description("Id of the slideshow to add the slide to.")] string slideshowId,
        [Description("Id of a template (from create_template/list_templates).")] string templateId,
        [Description("How long the slide stays on screen, in seconds.")] double? durationSeconds = null,
        [Description("Zero-based position in the loop; appended when omitted.")] int? position = null,
        CancellationToken ct = default) =>
        ToolResults.Execute(
            () => api.AddDynamicSlideAsync(
                slideshowId, new AddDynamicSlideRequest(templateId, durationSeconds, position), ct),
            InfoSlidesJsonContext.Default.Slide);

    [McpServerTool(Name = "upload_pptx")]
    [Description("Put an existing PowerPoint on a screen. Uploads a .pptx from the local disk and " +
                 "turns it into screen content — the server reads the slide count and the deck's own " +
                 "resolution and starts rendering it into a video stream. This is usually the quickest " +
                 "route when the user says they already have a presentation.")]
    public async Task<CallToolResult> UploadPptx(
        [Description("Absolute path to a .pptx file on disk.")] string filePath,
        [Description("Display name; defaults to the file name.")] string? title = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return ToolResults.ValidationError($"File not found: {filePath}");
        }

        await using var stream = File.OpenRead(filePath);
        return await ToolResults.Execute(
            () => api.UploadPptxAsync(stream, Path.GetFileName(filePath), title, ct),
            InfoSlidesJsonContext.Default.Slideshow);
    }

    [McpServerTool(Name = "set_slide_conditions")]
    [Description("Make a slide appear only at the right moment — the breakfast menu before 11, the " +
                 "weekend offer on Saturday and Sunday, a 'target hit' message only when the number is " +
                 "actually hit. Types: 'time' (e.g. '08:00-11:00'), 'weekday' (e.g. 'sat,sun'), " +
                 "'data_trigger' (e.g. 'sales_today > 1000000'). All listed conditions must hold. They " +
                 "are checked server-side as the stream renders; an empty list clears them.")]
    public Task<CallToolResult> SetSlideConditions(
        [Description("Id of the slide.")] string slideId,
        [Description("Conditions that must all hold for the slide to be shown.")] List<SlideCondition> conditions,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.SetSlideConditionsAsync(slideId, conditions, ct),
            InfoSlidesJsonContext.Default.Slide);

    [McpServerTool(Name = "preview_slide", ReadOnly = true)]
    [Description("See exactly what a slide will look like on the screen, as a PNG image, before the " +
                 "public does. Worth doing whenever text might overflow, a logo might sit badly, or " +
                 "live data has just been pushed — a mistake on a lobby display is visible to every " +
                 "person walking past.")]
    public Task<CallToolResult> PreviewSlide(
        [Description("Id of the slide to render.")] string slideId,
        CancellationToken ct = default) =>
        ToolResults.ExecutePng(() => api.GetSlidePreviewPngAsync(slideId, ct), $"Preview of slide {slideId}");
}
