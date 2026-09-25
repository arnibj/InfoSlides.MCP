using System.ComponentModel;
using System.Text.Json;
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
                 "and portrait, reorder the slides, turn the news ticker or the on-screen clock on or " +
                 "off, make every slide show for the same number of seconds, or force how it plays. " +
                 "Pass slideOrder as the complete list of slide ids in the sequence you want them " +
                 "shown. playbackMode overrides the usual rendered-video stream with a smooth HTML/CSS " +
                 "loop (or back again) for this one slideshow; pass \"inherit\" to drop the override and " +
                 "go back to whatever the workspace normally uses. Only what you pass changes. The " +
                 "screen picks the change up after a re-render: read the slideshow with get_slideshow " +
                 "until renderStatus is Completed before telling the person it is live.")]
    public Task<CallToolResult> UpdateSlideshow(
        [Description("Id of the slideshow to update.")] string slideshowId,
        [Description("New title, if changing.")] string? title = null,
        [Description("New width in pixels, if changing (requires height too).")] int? width = null,
        [Description("New height in pixels, if changing (requires width too).")] int? height = null,
        [Description("Complete ordered list of slide ids, if reordering.")] List<string>? slideOrder = null,
        [Description("\"VideoStream\" or \"Html\" to force how this slideshow plays; \"inherit\" to " +
                     "clear the override and fall back to the workspace default; omit to leave the " +
                     "current setting untouched.")]
        string? playbackMode = null,
        [Description("How many seconds each slide shows unless it has its own duration, 1 to 300. " +
                     "\"Make every slide show 15 seconds\" is this.")]
        int? defaultDurationSeconds = null,
        [Description("true shows the news ticker along the bottom of the screen, false turns it off " +
                     "(and clears its sources).")]
        bool? tickerEnabled = null,
        [Description("Content sources whose headlines scroll in the ticker (ids from list_sources).")]
        List<string>? tickerSourceIds = null,
        [Description("true shows the on-screen clock, false hides it.")] bool? clockEnabled = null,
        [Description("Where the clock sits: TopLeft, TopRight, BottomLeft or BottomRight.")] string? clockPosition = null,
        [Description("Whether the date shows under the time.")] bool? clockShowDate = null,
        [Description("Clock background as a hex colour, e.g. #000000 or #00000080.")] string? clockBackgroundColor = null,
        [Description("Clock text as a hex colour, e.g. #ffffff.")] string? clockTextColor = null,
        [Description("Whether other slideshows in the workspace may import slides from this one.")] bool? shared = null,
        CancellationToken ct = default)
    {
        if (width.HasValue != height.HasValue)
        {
            return Task.FromResult(ToolResults.ValidationError("Provide width and height together."));
        }

        var resolution = width.HasValue ? new Resolution(width.Value, height!.Value) : null;
        var ticker = tickerEnabled is null && tickerSourceIds is null ? null : new Ticker(tickerEnabled, tickerSourceIds);
        var clock = clockEnabled is null && clockPosition is null && clockShowDate is null
                    && clockBackgroundColor is null && clockTextColor is null
            ? null
            : new Clock(clockEnabled, clockPosition, clockShowDate, clockBackgroundColor, clockTextColor);
        return ToolResults.Execute(
            () => api.UpdateSlideshowAsync(
                slideshowId,
                new UpdateSlideshowRequest(title, resolution, slideOrder, playbackMode, defaultDurationSeconds, ticker, clock, shared),
                ct),
            InfoSlidesJsonContext.Default.Slideshow);
    }

    [McpServerTool(Name = "delete_slideshow", Destructive = true)]
    [Description("Delete a slideshow for good: the old lunch menu, a campaign that has ended, a test " +
                 "deck. Its schedules are removed and any screen playing it stops " +
                 "showing it at its next check-in. The API has no undelete, so confirm with the person " +
                 "first unless they clearly asked for it. To take one slide out, use delete_slide or " +
                 "hide it with update_slide instead.")]
    public Task<CallToolResult> DeleteSlideshow(
        [Description("Id of the slideshow to delete.")] string slideshowId,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.DeleteSlideshowAsync(slideshowId, ct), InfoSlidesJsonContext.Default.OkResult);

    [McpServerTool(Name = "replace_slideshow_file", Destructive = true)]
    [Description("Swap the PowerPoint or PDF behind an existing slideshow for a new version, so " +
                 "\"replace the lobby presentation with this new PowerPoint\" keeps the same slideshow, " +
                 "the same screens and the same schedule. Photos, videos and live-data slides the " +
                 "slideshow already has are kept. Read it back with get_slideshow to check the pages. " +
                 "The screen updates once the re-render finishes (renderStatus: Completed).")]
    public async Task<CallToolResult> ReplaceSlideshowFile(
        [Description("Id of the slideshow whose file to replace.")] string slideshowId,
        [Description("Absolute path to the new .pptx or .pdf file on disk.")] string filePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return ToolResults.ValidationError($"File not found: {filePath}");
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension is not (".pptx" or ".pdf"))
        {
            return ToolResults.ValidationError($"Unsupported file type '{extension}'; expected .pptx or .pdf.");
        }

        await using var stream = File.OpenRead(filePath);
        return await ToolResults.Execute(
            () => api.ReplaceSlideshowFileAsync(slideshowId, stream, Path.GetFileName(filePath), ct),
            InfoSlidesJsonContext.Default.Slideshow);
    }

    [McpServerTool(Name = "list_slideshows", ReadOnly = true)]
    [Description("See everything this workspace can put on a screen, with the shape (landscape or " +
                 "portrait) each one is built for, whether its latest render has finished " +
                 "(renderStatus) and how many screens are playing it (screenCount). Use it to find the " +
                 "id of existing content before editing it or assigning it to a display; to go from a " +
                 "screen's name to its content, list_devices shows what each screen is playing.")]
    public Task<CallToolResult> ListSlideshows(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ListSlideshowsAsync(ct), InfoSlidesJsonContext.Default.ListSlideshow);

    [McpServerTool(Name = "get_slideshow", ReadOnly = true)]
    [Description("Look inside one piece of screen content: every slide (its type, whether it is " +
                 "hidden, how long it is shown, a thumbnailUrl, and every rule about when it appears), " +
                 "the order they play in, the ticker, clock and default duration, which screens are " +
                 "playing it (screens, each with a playerUrl to hand to the person) and renderStatus. " +
                 "Use this before editing so you know what is already there, and again after an edit: " +
                 "the change is on the screen once renderStatus is Completed. To look at a slide, " +
                 "call preview_slide.")]
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
                 "Needs a template first (see create_template or list_templates). With a push template " +
                 "the result includes sourceId, the push source to send data to with push_data, and " +
                 "with createPushKey=true a pushKey for the user's system (shown once); the slide " +
                 "stays hidden until the first data arrives. Otherwise push values with update_source.")]
    public Task<CallToolResult> AddDynamicSlide(
        [Description("Id of the slideshow to add the slide to.")] string slideshowId,
        [Description("Id of a template (from create_template/list_templates).")] string templateId,
        [Description("How long the slide stays on screen, in seconds.")] double? durationSeconds = null,
        [Description("Zero-based position in the loop; appended when omitted.")] int? position = null,
        [Description("For a push template: also create a push-only key for the new push source.")] bool createPushKey = false,
        CancellationToken ct = default) =>
        ToolResults.Execute(
            () => api.AddDynamicSlideAsync(
                slideshowId, new AddDynamicSlideRequest(templateId, durationSeconds, position, createPushKey ? true : null), ct),
            InfoSlidesJsonContext.Default.Slide);

    [McpServerTool(Name = "upload_pptx")]
    [Description("Put an existing PowerPoint or PDF on a screen. Uploads a .pptx or .pdf from the " +
                 "local disk and turns it into screen content — the server reads the slide/page count " +
                 "and the file's own resolution and starts rendering it into a video stream. This is " +
                 "usually the quickest route when the user says they already have a presentation or a " +
                 "PDF deck.")]
    public async Task<CallToolResult> UploadPptx(
        [Description("Absolute path to a .pptx or .pdf file on disk.")] string filePath,
        [Description("Display name; defaults to the file name.")] string? title = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return ToolResults.ValidationError($"File not found: {filePath}");
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension is not (".pptx" or ".pdf"))
        {
            return ToolResults.ValidationError($"Unsupported file type '{extension}'; expected .pptx or .pdf.");
        }

        await using var stream = File.OpenRead(filePath);
        return await ToolResults.Execute(
            () => api.UploadPptxAsync(stream, Path.GetFileName(filePath), title, ct),
            InfoSlidesJsonContext.Default.Slideshow);
    }

    [McpServerTool(Name = "set_slide_conditions")]
    [Description("Make a slide appear only at the right moment, or vanish at one, like the breakfast menu " +
                 "before 11, the weekend offer on Saturday and Sunday, the Christmas slide only in " +
                 "December, a 'target hit' message only when the number is actually hit. Types: " +
                 "'time' (e.g. '08:00-11:00'), 'weekday' (e.g. 'sat,sun'), 'date' (e.g. " +
                 "'2026-12-01..2026-12-26'; either end may be left out), 'data_trigger' (e.g. " +
                 "'sales_today > 1000000'). By default the slide is shown only while ALL the " +
                 "conditions hold; match='any' shows it while at least one does, and mode='hide' " +
                 "turns the rule around so the slide is hidden while they hold. Checked server-side as " +
                 "the stream renders; an empty list clears the slide's rule. get_slideshow reads " +
                 "every rule back; ones marked readOnly were set on the web page and are not " +
                 "replaced by this call. To hide a slide unconditionally use update_slide.")]
    public Task<CallToolResult> SetSlideConditions(
        [Description("Id of the slide.")] string slideId,
        [Description("The conditions. Empty list clears them.")] List<SlideCondition> conditions,
        [Description("\"show\" (default): show the slide only while the conditions hold. \"hide\": hide it while they hold.")]
        string? mode = null,
        [Description("\"all\" (default): every condition must hold. \"any\": one is enough.")]
        string? match = null,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.SetSlideConditionsAsync(slideId, conditions, mode, match, ct),
            InfoSlidesJsonContext.Default.Slide);

    [McpServerTool(Name = "update_slide")]
    [Description("Change one slide that is already in a slideshow: make it stay on screen longer or " +
                 "shorter (durationSeconds, 1 to 300), or hide it and bring it back (hidden), like \"hide " +
                 "the Christmas slide\", \"show the offer for 20 seconds\". For a live-data slide it " +
                 "can also change what it shows: switch to another template (templateId) or content " +
                 "source (sourceId), change the value on it (overrideData, e.g. {\"price\":\"1.990 kr\"}; " +
                 "only the fields you send change, and null removes one), or set its countdown. Text " +
                 "inside an uploaded PowerPoint page cannot be edited this way: say so, and offer to " +
                 "replace the file with replace_slideshow_file. Only what you pass changes. To take " +
                 "the slide out for good use delete_slide.")]
    public Task<CallToolResult> UpdateSlide(
        [Description("Id of the slide.")] string slideId,
        [Description("How long the slide stays on screen, in seconds (1 to 300).")] double? durationSeconds = null,
        [Description("true hides the slide from playback, false shows it again.")] bool? hidden = null,
        [Description("Dynamic slides only: id of the template to switch to (from list_templates).")] string? templateId = null,
        [Description("Dynamic slides only: id of the content source to take data from (from list_sources).")] string? sourceId = null,
        [Description("Dynamic slides only: template field to source path mapping, replacing the current one. " +
                     "Generated automatically when the template or source changes and this is left out.")]
        JsonElement? fieldMapping = null,
        [Description("Dynamic slides only: values to change on the slide, as a JSON object with the template's " +
                     "field names. Merged into the current values; a null value removes a field.")]
        JsonElement? overrideData = null,
        [Description("Dynamic slides only: a countdown object (or array of them); [] clears the countdowns.")]
        JsonElement? countdown = null,
        CancellationToken ct = default)
    {
        if (durationSeconds is null && hidden is null && templateId is null && sourceId is null
            && fieldMapping is null && overrideData is null && countdown is null)
        {
            return Task.FromResult(ToolResults.ValidationError("Pass at least one field to change."));
        }

        return ToolResults.Execute(
            () => api.UpdateSlideAsync(
                slideId,
                new UpdateSlideRequest(durationSeconds, hidden, templateId, sourceId, fieldMapping, overrideData, countdown),
                ct),
            InfoSlidesJsonContext.Default.Slide);
    }

    [McpServerTool(Name = "delete_slide", Destructive = true)]
    [Description("Remove one slide from a slideshow for good: the Christmas slide once it is over, a " +
                 "wrong photo, a page nobody wants. The slides after it close up and any rule on it " +
                 "goes too. If the slide may come back later, hide it with update_slide instead. " +
                 "Confirm with the person first unless they clearly asked for it.")]
    public Task<CallToolResult> DeleteSlide(
        [Description("Id of the slide to remove.")] string slideId,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.DeleteSlideAsync(slideId, ct), InfoSlidesJsonContext.Default.OkResult);

    [McpServerTool(Name = "list_sources", ReadOnly = true)]
    [Description("See the data sources this workspace can show on a screen: RSS news feeds, " +
                 "calendars, weather, live data pushed by another system. Use it to find the id of a " +
                 "source before scrolling its headlines in a slideshow's news ticker " +
                 "(update_slideshow tickerSourceIds) or pointing a live-data slide at it (update_slide " +
                 "sourceId). Each entry has its name, type and when it last received data.")]
    public Task<CallToolResult> ListSources(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ListSourcesAsync(ct), InfoSlidesJsonContext.Default.ListSource);

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
