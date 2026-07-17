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
    [Description("Create a slideshow (deck). Resolution defaults to 1920x1080; use 1080x1920 for portrait " +
                 "screens. Slides can be added inline or later via add_media_slide.")]
    public Task<CallToolResult> UploadSlideshow(
        [Description("Slideshow title.")] string title,
        [Description("Horizontal resolution in pixels (default 1920).")] int width = 1920,
        [Description("Vertical resolution in pixels (default 1080).")] int height = 1080,
        [Description("Optional initial slides (mediaUrl and/or templateId, optional durationSeconds).")]
        List<NewSlide>? slides = null,
        CancellationToken ct = default) =>
        ToolResults.Execute(
            () => api.CreateSlideshowAsync(new CreateSlideshowRequest(title, new Resolution(width, height), slides), ct),
            InfoSlidesJsonContext.Default.Slideshow);

    [McpServerTool(Name = "update_slideshow")]
    [Description("Update a slideshow's title, resolution, or slide order (pass slideOrder as the full " +
                 "list of slide ids in the desired sequence).")]
    public Task<CallToolResult> UpdateSlideshow(
        [Description("Id of the slideshow to update.")] string slideshowId,
        [Description("New title, if changing.")] string? title = null,
        [Description("New horizontal resolution, if changing (requires height too).")] int? width = null,
        [Description("New vertical resolution, if changing (requires width too).")] int? height = null,
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
    [Description("List all slideshows in the tenant with their resolutions.")]
    public Task<CallToolResult> ListSlideshows(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ListSlideshowsAsync(ct), InfoSlidesJsonContext.Default.ListSlideshow);

    [McpServerTool(Name = "get_slideshow", ReadOnly = true)]
    [Description("Get one slideshow including its slides, their order, durations and visibility conditions.")]
    public Task<CallToolResult> GetSlideshow(
        [Description("Id of the slideshow.")] string slideshowId,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.GetSlideshowAsync(slideshowId, ct), InfoSlidesJsonContext.Default.Slideshow);

    [McpServerTool(Name = "clone_slideshow")]
    [Description("Clone a slideshow into a new one. Set fromGallery=true to clone a starter-gallery deck " +
                 "(see list_gallery) — the fastest way to get a beautiful deck to customize.")]
    public Task<CallToolResult> CloneSlideshow(
        [Description("Id of the source slideshow, or gallery item id when fromGallery=true.")] string sourceId,
        [Description("Clone from the starter gallery instead of an existing tenant slideshow.")] bool fromGallery = false,
        CancellationToken ct = default) =>
        ToolResults.Execute(
            () => fromGallery ? api.CloneGalleryItemAsync(sourceId, ct) : api.CloneSlideshowAsync(sourceId, ct),
            InfoSlidesJsonContext.Default.Slideshow);

    [McpServerTool(Name = "list_gallery", ReadOnly = true)]
    [Description("List the starter gallery of pre-built decks that can be cloned with " +
                 "clone_slideshow(fromGallery=true).")]
    public Task<CallToolResult> ListGallery(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ListGalleryAsync(ct), InfoSlidesJsonContext.Default.ListGalleryItem);

    [McpServerTool(Name = "add_media_slide")]
    [Description("Add a media slide (image or video URL) to a slideshow. Returns an AspectMismatch warning " +
                 "when the media ratio differs from the slideshow resolution — self-correct if it appears.")]
    public Task<CallToolResult> AddMediaSlide(
        [Description("Id of the slideshow to add the slide to.")] string slideshowId,
        [Description("Publicly reachable URL of the image/video.")] string mediaUrl,
        [Description("How long the slide is shown, in seconds.")] double? durationSeconds = null,
        [Description("Zero-based position in the deck; appended when omitted.")] int? position = null,
        CancellationToken ct = default) =>
        ToolResults.Execute(
            () => api.AddMediaSlideAsync(slideshowId, new AddMediaSlideRequest(mediaUrl, durationSeconds, position), ct),
            InfoSlidesJsonContext.Default.Slide);

    [McpServerTool(Name = "set_slide_conditions")]
    [Description("Replace the visibility conditions of a slide. Types: 'time' (e.g. '08:00-11:00'), " +
                 "'weekday' (e.g. 'sat,sun'), 'data_trigger' (e.g. 'sales_today > 1000000'). Conditions are " +
                 "evaluated server-side during stream rendering; an empty list clears all conditions.")]
    public Task<CallToolResult> SetSlideConditions(
        [Description("Id of the slide.")] string slideId,
        [Description("Conditions that must all hold for the slide to be shown.")] List<SlideCondition> conditions,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.SetSlideConditionsAsync(slideId, conditions, ct),
            InfoSlidesJsonContext.Default.Slide);

    [McpServerTool(Name = "preview_slide", ReadOnly = true)]
    [Description("Render a slide to a PNG image and return it, so you can visually verify content and " +
                 "layout before it reaches a live screen.")]
    public Task<CallToolResult> PreviewSlide(
        [Description("Id of the slide to render.")] string slideId,
        CancellationToken ct = default) =>
        ToolResults.ExecutePng(() => api.GetSlidePreviewPngAsync(slideId, ct), $"Preview of slide {slideId}");
}
