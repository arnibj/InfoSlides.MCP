using System.CommandLine;
using InfoSlides.Mcp.Tools;

namespace InfoSlides.Cli.Commands;

/// <summary>
/// CLI verbs for the MCP tools on the pass-through client (screens, schedules, takeovers, team,
/// workspace, sources, AI Studio, undo). Each verb calls the tool method itself through
/// <see cref="CliContext.RunTool"/>, so the CLI and the MCP server cannot drift apart.
/// </summary>
internal static class ToolCommands
{
    private static Argument<string> Arg(string name, string description) => new(name) { Description = description };

    private static Option<T> Opt<T>(string name, string description) => new(name) { Description = description };

    /// <summary>Repeatable, comma-separable id option (<c>--device a,b --device c</c>).</summary>
    private static Option<string[]> Ids(string name, string description) =>
        new(name) { Description = description, AllowMultipleArgumentsPerToken = true };

    private static List<string>? Split(string[]? values) =>
        values?.SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToList()
            is { Count: > 0 } ids ? ids : null;

    private static Command Verb(string name, string description, Func<ParseResult, CancellationToken, Task<int>> action, params Symbol[] symbols)
    {
        var command = new Command(name, description);
        foreach (var symbol in symbols)
        {
            switch (symbol)
            {
                case Argument argument:
                    command.Arguments.Add(argument);
                    break;
                case Option option:
                    command.Options.Add(option);
                    break;
            }
        }

        command.SetAction(action);
        return command;
    }

    /// <summary>Adds pairing, play, identify, what's on, diagnosis, show, rename, location, alerts and play time to <c>device</c>.</summary>
    /// <param name="device">The <c>device</c> command.</param>
    public static void AddDeviceVerbs(Command device)
    {
        var qr = Opt<string?>("--qr", "The scanned pairing QR code URL or session id.");
        var nickname = Opt<string?>("--nickname", "The nickname under the QR code, e.g. swift-oak-42.");
        var pairDevice = Opt<string?>("--device-id", "Existing screen: alone, prints a code to type on the TV; with --qr/--nickname, binds the TV to it.");
        var pairSlideshow = Opt<string?>("--slideshow-id", "With --qr/--nickname: a new screen playing this slideshow.");
        var pairName = Opt<string?>("--name", "Name for a newly created screen.");
        var pairDry = Opt<bool>("--dry-run", "Validate without pairing anything.");
        device.Subcommands.Add(Verb("pair", "Pair a TV showing the pairing screen, or get a code to type on it.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).PairScreen(p.GetValue(qr), p.GetValue(nickname),
                p.GetValue(pairDevice), p.GetValue(pairSlideshow), p.GetValue(pairName), p.GetValue(pairDry), ct)),
            qr, nickname, pairDevice, pairSlideshow, pairName, pairDry));

        var playShow = Arg("slideshow-id", "The slideshow to play.");
        var playDevice = Opt<string?>("--device-id", "The screen; optional with one screen.");
        var playUntil = Opt<string?>("--until", "End time (workspace local time, or with Z/offset); makes it a takeover.");
        var playDry = Opt<bool>("--dry-run", "Report what would happen without changing anything.");
        device.Subcommands.Add(Verb("play", "Play a slideshow on a screen now.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).PlayNow(p.GetValue(playShow)!, p.GetValue(playDevice),
                p.GetValue(playUntil), p.GetValue(playDry), ct)),
            playShow, playDevice, playUntil, playDry));

        var identifyIds = new Argument<string[]>("device-ids") { Description = "Only these screens; all online screens when omitted.", Arity = ArgumentArity.ZeroOrMore };
        device.Subcommands.Add(Verb("identify", "Show each screen's name on it for about 90 seconds to tell them apart.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).IdentifyScreens(Split(p.GetValue(identifyIds)), ct)),
            identifyIds));

        var nowId = Arg("device-id", "Screen id.");
        var nowOutput = Opt<string?>("--output", "Output PNG path (default screen-<id>.png).");
        device.Subcommands.Add(Verb("now", "Save the slide a screen should be showing right now as a PNG.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).GetScreenNow(p.GetValue(nowId)!, ct),
                p.GetValue(nowOutput) ?? $"screen-{p.GetValue(nowId)}.png"),
            nowId, nowOutput));

        var diagnoseId = Arg("device-id", "Screen id.");
        device.Subcommands.Add(Verb("diagnose", "List why a screen may look wrong, most likely first.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).DiagnoseScreen(p.GetValue(diagnoseId)!, ct)),
            diagnoseId));

        var showId = Arg("device-id", "Screen id.");
        var showFile = Opt<string?>("--file", "A photo or video on disk.");
        var showUrl = Opt<string?>("--url", "A public photo or video address instead.");
        var showUntil = Opt<string?>("--until", "End time; default 30 minutes from now.");
        var showCaption = Opt<string?>("--caption", "Name for the slideshow that is created.");
        device.Subcommands.Add(Verb("show", "Show a photo or video on a screen as a temporary takeover.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).ShowOnScreen(p.GetValue(showId)!, p.GetValue(showFile),
                p.GetValue(showUrl), p.GetValue(showUntil), p.GetValue(showCaption), ct)),
            showId, showFile, showUrl, showUntil, showCaption));

        var statusDevice = Arg("device-id", "Screen id.");
        var statusMedia = Arg("media-asset-id", "mediaAssetId from `device show`.");
        var statusShow = Arg("slideshow-id", "slideshowId from `device show`.");
        device.Subcommands.Add(Verb("show-status", "Progress of a photo or video sent with `device show`.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).GetShowStatus(p.GetValue(statusDevice)!,
                p.GetValue(statusMedia)!, p.GetValue(statusShow)!, ct)),
            statusDevice, statusMedia, statusShow));

        var updateId = Arg("device-id", "Screen id.");
        var updateName = Opt<string?>("--name", "New name.");
        var updateWidth = Opt<int?>("--width", "New width; give with --height.");
        var updateHeight = Opt<int?>("--height", "New height; give with --width.");
        device.Subcommands.Add(Verb("update", "Rename a screen or change its resolution.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).UpdateDevice(p.GetValue(updateId)!, p.GetValue(updateName),
                p.GetValue(updateWidth), p.GetValue(updateHeight), ct: ct)),
            updateId, updateName, updateWidth, updateHeight));

        var locationId = Arg("device-id", "Screen id.");
        var latitude = new Argument<double>("latitude") { Description = "Latitude in degrees." };
        var longitude = new Argument<double>("longitude") { Description = "Longitude in degrees." };
        var locationName = Opt<string?>("--label", "Label, e.g. 'Front lobby entrance'.");
        device.Subcommands.Add(Verb("set-location", "Save where a screen is, for `device list --near`.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).UpdateDevice(p.GetValue(locationId)!,
                latitude: p.GetValue(latitude), longitude: p.GetValue(longitude), locationName: p.GetValue(locationName), ct: ct)),
            locationId, latitude, longitude, locationName));

        var alertsId = Arg("device-id", "Screen id.");
        var alertsEnabled = new Argument<bool>("enabled") { Description = "false stops the offline emails for this screen." };
        var alertsStart = Opt<string?>("--quiet-start", "Own quiet hours start, HH:mm; give with --quiet-end.");
        var alertsEnd = Opt<string?>("--quiet-end", "Own quiet hours end, HH:mm.");
        device.Subcommands.Add(Verb("offline-alerts", "Turn a screen's offline emails on or off, with optional own quiet hours.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).SetScreenOfflineAlerts(p.GetValue(alertsId)!,
                p.GetValue(alertsEnabled), p.GetValue(alertsStart), p.GetValue(alertsEnd), ct)),
            alertsId, alertsEnabled, alertsStart, alertsEnd));

        var playsId = Arg("device-id", "Screen id.");
        var (from, to) = (Opt<string?>("--from", "Start date, yyyy-MM-dd."), Opt<string?>("--to", "End date, yyyy-MM-dd."));
        device.Subcommands.Add(Verb("plays", "Estimated play time per slideshow on a screen.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).GetDevicePlays(p.GetValue(playsId)!, p.GetValue(from)!, p.GetValue(to)!, ct)),
            playsId, from, to));

        var deleteId = Arg("device-id", "Screen id.");
        device.Subcommands.Add(Verb("delete", "Remove a screen from the workspace; prints an undo token.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).DeleteDevice(p.GetValue(deleteId)!, ct)),
            deleteId));

        var broadcastName = Arg("name", "Name of the channel.");
        var broadcastShow = Opt<string?>("--slideshow-id", "The slideshow it plays.");
        device.Subcommands.Add(Verb("broadcast", "Create a broadcast channel: one stream for any number of screens, no pairing.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).CreateBroadcastDevice(p.GetValue(broadcastName)!, p.GetValue(broadcastShow), ct)),
            broadcastName, broadcastShow));
    }

    /// <summary>Adds play time, restore, Google import, shared slides and the weather alert timeline to <c>slideshow</c>.</summary>
    /// <param name="slideshow">The <c>slideshow</c> command.</param>
    public static void AddSlideshowVerbs(Command slideshow)
    {
        var id = Arg("slideshow-id", "Slideshow id.");
        var (from, to) = (Opt<string?>("--from", "Start date, yyyy-MM-dd."), Opt<string?>("--to", "End date, yyyy-MM-dd."));
        slideshow.Subcommands.Add(Verb("plays", "Estimated play time per screen for a slideshow.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).GetSlideshowPlays(p.GetValue(id)!, p.GetValue(from)!, p.GetValue(to)!, ct)),
            id, from, to));

        var restoreId = Arg("slideshow-id", "Id of the deleted slideshow (see `workspace audit --action SlideshowDeleted`).");
        slideshow.Subcommands.Add(Verb("restore", "Bring back a deleted slideshow; assign it to a screen again afterwards.",
            (p, ct) => CliContext.RunTool(p, api => new SlideshowTools(api).RestoreSlideshow(p.GetValue(restoreId)!, ct)),
            restoreId));

        var token = Arg("access-token", "Google access token with Drive read access to the file.");
        var fileId = Opt<string?>("--file-id", "Google Drive file id.");
        var url = Opt<string?>("--url", "Presentation address.");
        var title = Opt<string?>("--title", "Slideshow title; the Google file name when omitted.");
        slideshow.Subcommands.Add(Verb("import-google", "Make a slideshow from a Google Slides presentation.",
            (p, ct) => CliContext.RunTool(p, api => new SlideshowTools(api).ImportGoogleSlides(p.GetValue(token)!, p.GetValue(fileId),
                p.GetValue(url), p.GetValue(title), ct)),
            token, fileId, url, title));

        var exclude = Opt<string?>("--exclude-id", "Leave this slideshow out.");
        slideshow.Subcommands.Add(Verb("shared", "List shared slideshows and the pages that can be copied.",
            (p, ct) => CliContext.RunTool(p, api => new SlideshowTools(api).ListSharedSlideshows(p.GetValue(exclude), ct)),
            exclude));

        var importTarget = Arg("slideshow-id", "The slideshow that receives the page.");
        var importSource = Arg("source-slideshow-id", "The shared slideshow to copy from.");
        var importPage = new Argument<int>("slide-number") { Description = "The page number to copy." };
        slideshow.Subcommands.Add(Verb("import-slide", "Copy one page of a shared slideshow to the end of this one.",
            (p, ct) => CliContext.RunTool(p, api => new SlideshowTools(api).ImportSlide(p.GetValue(importTarget)!, p.GetValue(importSource)!,
                p.GetValue(importPage), ct)),
            importTarget, importSource, importPage));

        var alertsShow = Arg("slideshow-id", "Slideshow id.");
        var alertsSource = Arg("source-id", "A WeatherAlert source.");
        var alertsPosition = Opt<int?>("--position", "Zero-based position of the first slide.");
        var alertsDuration = Opt<int?>("--duration", "Seconds per slide (default 10).");
        slideshow.Subcommands.Add(Verb("weather-alerts", "Add an 8-slide weather alert timeline over the next 48 hours.",
            (p, ct) => CliContext.RunTool(p, api => new SlideshowTools(api).AddWeatherAlertTimeline(p.GetValue(alertsShow)!, p.GetValue(alertsSource)!,
                p.GetValue(alertsPosition), p.GetValue(alertsDuration), null, null, ct)),
            alertsShow, alertsSource, alertsPosition, alertsDuration));
    }

    /// <summary>Adds reading and changing a text slide's design, and the stock photo search, to <c>slide</c>.</summary>
    /// <param name="slide">The <c>slide</c> command.</param>
    public static void AddSlideVerbs(Command slide)
    {
        var designId = Arg("slide-id", "Slide id.");
        slide.Subcommands.Add(Verb("design", "Show the design behind a text slide.",
            (p, ct) => CliContext.RunTool(p, api => new SlideshowTools(api).GetSlideDesign(p.GetValue(designId)!, ct)),
            designId));

        var setId = Arg("slide-id", "Slide id.");
        var design = Arg("design", "The whole design as a JSON object, inline or @file.");
        slide.Subcommands.Add(Verb("set-design", "Replace a text slide's design; prints an undo token.",
            (p, ct) => CliContext.RunTool(p, api => new SlideshowTools(api).UpdateSlideDesign(p.GetValue(setId)!,
                CliContext.ParseJsonObject(p.GetValue(design)!), ct)),
            setId, design));

        var q = Arg("query", "What the photo should show.");
        var orientation = Opt<string?>("--orientation", "landscape (default), portrait or squarish.");
        var page = Opt<int?>("--page", "Page number.");
        slide.Subcommands.Add(Verb("stock-search", "Search free stock photos (Unsplash) for a text slide background.",
            (p, ct) => CliContext.RunTool(p, api => new SlideshowTools(api).SearchStockImages(p.GetValue(q)!, p.GetValue(orientation), p.GetValue(page), ct)),
            q, orientation, page));
    }

    /// <summary>Adds limits, browsing, changing and deleting files, and folders to <c>media</c>.</summary>
    /// <param name="media">The <c>media</c> command.</param>
    public static void AddMediaVerbs(Command media)
    {
        media.Subcommands.Add(Verb("limits", "Free storage, largest file and accepted file types.",
            (p, ct) => CliContext.RunTool(p, api => new MediaTools(api).GetUploadLimits(ct))));

        var folder = Opt<string?>("--folder-id", "Only files in this folder.");
        var type = Opt<string?>("--type", "image, video or document.");
        var name = Opt<string?>("--name", "Only names containing this.");
        var page = Opt<int?>("--page", "Page number.");
        media.Subcommands.Add(Verb("list", "List the media library, newest first.",
            (p, ct) => CliContext.RunTool(p, api => new MediaTools(api).ListMedia(p.GetValue(folder), p.GetValue(type), p.GetValue(name), p.GetValue(page), null, ct)),
            folder, type, name, page));

        var getId = Arg("media-id", "Media file id.");
        media.Subcommands.Add(Verb("status", "Whether an uploaded file is ready (video progress while it is prepared).",
            (p, ct) => CliContext.RunTool(p, api => new MediaTools(api).GetMedia(p.GetValue(getId)!, ct)),
            getId));

        var editId = Arg("media-id", "Media file id.");
        var editName = Opt<string?>("--name", "New name.");
        var editFolder = Opt<string?>("--folder-id", "Move it to this folder.");
        var root = Opt<bool>("--root", "Move it to the top level.");
        var shared = Opt<bool?>("--shared", "true shares it with other workspaces, false stops.");
        media.Subcommands.Add(Verb("edit", "Rename, move or share a file.",
            (p, ct) => CliContext.RunTool(p, api => new MediaTools(api).UpdateMedia(p.GetValue(editId)!, p.GetValue(editName),
                p.GetValue(editFolder), p.GetValue(root), p.GetValue(shared), ct)),
            editId, editName, editFolder, root, shared));

        var deleteId = Arg("media-id", "Media file id.");
        media.Subcommands.Add(Verb("delete", "Delete a file; refused while a slide uses it.",
            (p, ct) => CliContext.RunTool(p, api => new MediaTools(api).DeleteMedia(p.GetValue(deleteId)!, ct)),
            deleteId));

        media.Subcommands.Add(Verb("folders", "List media folders.",
            (p, ct) => CliContext.RunTool(p, api => new MediaTools(api).ListMediaFolders(ct))));

        var folderName = Arg("name", "Folder name.");
        var parent = Opt<string?>("--parent-id", "Create it inside this folder.");
        media.Subcommands.Add(Verb("folder-create", "Create a media folder.",
            (p, ct) => CliContext.RunTool(p, api => new MediaTools(api).CreateMediaFolder(p.GetValue(folderName)!, p.GetValue(parent), ct)),
            folderName, parent));

        var renameId = Arg("folder-id", "Folder id.");
        var newName = Arg("name", "New name.");
        media.Subcommands.Add(Verb("folder-rename", "Rename a media folder.",
            (p, ct) => CliContext.RunTool(p, api => new MediaTools(api).RenameMediaFolder(p.GetValue(renameId)!, p.GetValue(newName)!, ct)),
            renameId, newName));

        var folderDelete = Arg("folder-id", "Folder id.");
        media.Subcommands.Add(Verb("folder-delete", "Delete an empty media folder.",
            (p, ct) => CliContext.RunTool(p, api => new MediaTools(api).DeleteMediaFolder(p.GetValue(folderDelete)!, ct)),
            folderDelete));
    }

    /// <summary>Adds viewing and editing timed entries to <c>schedule</c>.</summary>
    /// <param name="schedule">The <c>schedule</c> command.</param>
    public static void AddScheduleVerbs(Command schedule)
    {
        var showId = Arg("device-id", "Screen id.");
        schedule.Subcommands.Add(Verb("show", "Show a screen's default content and timed entries.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).GetSchedule(p.GetValue(showId)!, ct)),
            showId));

        var addDevice = Arg("device-id", "Screen id.");
        var addShow = Arg("slideshow-id", "Slideshow to play in the window.");
        var start = Arg("start", "Start time, HH:mm (workspace time zone).");
        var end = Arg("end", "End time, HH:mm.");
        var priority = Opt<int>("--priority", "Priority where windows overlap (default 0).");
        schedule.Subcommands.Add(Verb("add", "Play a slideshow in a daily time window, e.g. 06:00 11:00.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).AddScheduleEntry(p.GetValue(addDevice)!, p.GetValue(addShow)!,
                p.GetValue(start)!, p.GetValue(end)!, p.GetValue(priority), ct)),
            addDevice, addShow, start, end, priority));

        var removeDevice = Arg("device-id", "Screen id.");
        var entry = Arg("entry-id", "Entry id from `schedule show`.");
        schedule.Subcommands.Add(Verb("remove", "Remove a timed entry.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).DeleteScheduleEntry(p.GetValue(removeDevice)!, p.GetValue(entry)!, ct)),
            removeDevice, entry));

        var editDevice = Arg("device-id", "Screen id.");
        var editEntry = Arg("entry-id", "Entry id from `schedule show`.");
        var editShow = Opt<string?>("--slideshow-id", "Play this slideshow in the window instead.");
        var editStart = Opt<string?>("--start", "New start, HH:mm.");
        var editEnd = Opt<string?>("--end", "New end, HH:mm.");
        var editPriority = Opt<int?>("--priority", "New priority.");
        schedule.Subcommands.Add(Verb("edit", "Change a timed entry in place.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).UpdateScheduleEntry(p.GetValue(editDevice)!, p.GetValue(editEntry)!,
                p.GetValue(editShow), p.GetValue(editStart), p.GetValue(editEnd), p.GetValue(editPriority), ct)),
            editDevice, editEntry, editShow, editStart, editEnd, editPriority));
    }

    /// <summary>Adds adapters and creating, editing and deleting pulled sources to <c>source</c>.</summary>
    /// <param name="source">The <c>source</c> command.</param>
    public static void AddSourceVerbs(Command source)
    {
        source.Subcommands.Add(Verb("adapters", "List the kinds of source that can be created, with their config fields.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).ListAdapters(ct))));

        var adapter = Arg("adapter-type", "Adapter type from `source adapters`, e.g. RssFeed.");
        var name = Arg("name", "Display name.");
        var config = Opt<string?>("--config", "Config JSON object, inline or @file.");
        var interval = Opt<int?>("--interval", "Fetch interval in seconds (default 3600).");
        source.Subcommands.Add(Verb("create", "Create a source InfoSlides fetches itself (RSS, weather, calendar...).",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).CreateSource(p.GetValue(adapter)!, p.GetValue(name)!,
                p.GetValue(config) is { } c ? CliContext.ParseJsonObject(c) : null, p.GetValue(interval), ct)),
            adapter, name, config, interval));

        var editId = Arg("source-id", "Source id.");
        var editName = Opt<string?>("--name", "New display name.");
        var editConfig = Opt<string?>("--config", "New config JSON object, inline or @file.");
        var editInterval = Opt<int?>("--interval", "New fetch interval in seconds.");
        var enabled = Opt<bool?>("--enabled", "false pauses fetching, true resumes it.");
        source.Subcommands.Add(Verb("edit", "Change a source's name, config or interval, or pause and resume it.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).EditSource(p.GetValue(editId)!, p.GetValue(editName),
                p.GetValue(editConfig) is { } c ? CliContext.ParseJsonObject(c) : null, p.GetValue(editInterval), p.GetValue(enabled), ct)),
            editId, editName, editConfig, editInterval, enabled));

        var deleteId = Arg("source-id", "Source id.");
        source.Subcommands.Add(Verb("delete", "Delete a source; slides and tickers fed by it stop updating.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).DeleteSource(p.GetValue(deleteId)!, ct)),
            deleteId));

        var fetchId = Arg("source-id", "Source id.");
        source.Subcommands.Add(Verb("fetch", "Refresh a source now instead of waiting for its next fetch.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).FetchSourceNow(p.GetValue(fetchId)!, ct)),
            fetchId));

        var itemsId = Arg("source-id", "Source id.");
        var itemsLimit = Opt<int?>("--limit", "How many items, 1 to 100 (default 20).");
        source.Subcommands.Add(Verb("items", "Show the latest items a source has fetched.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).GetSourceItems(p.GetValue(itemsId)!, p.GetValue(itemsLimit), ct)),
            itemsId, itemsLimit));

        var testAdapter = Arg("adapter-type", "Adapter type from `source adapters`, e.g. RssFeed.");
        var testConfig = Opt<string?>("--config", "Config JSON object, inline or @file.");
        source.Subcommands.Add(Verb("test", "Try source settings against the live service without saving anything.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).TestSourceConfig(p.GetValue(testAdapter)!,
                p.GetValue(testConfig) is { } c ? CliContext.ParseJsonObject(c) : null, ct)),
            testAdapter, testConfig));

        var fieldsId = Arg("source-id", "Source id.");
        source.Subcommands.Add(Verb("fields", "List the fields a source's items have, with a sample value.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).GetSourceFields(p.GetValue(fieldsId)!, ct)),
            fieldsId));
    }

    /// <summary>Adds previewing an unsaved design to <c>template</c>.</summary>
    /// <param name="template">The <c>template</c> command.</param>
    public static void AddTemplateVerbs(Command template)
    {
        var html = Arg("html", "Template HTML, inline or @file.");
        var css = Opt<string?>("--css", "Template CSS, inline or @file.");
        var sample = Opt<string?>("--sample-data", "Example data JSON object, inline or @file.");
        var aspect = Opt<string?>("--aspect-ratio", "16:9 (default), 9:16 or 1:1.");
        var output = Opt<string?>("--output", "Output PNG path (default template-preview.png).");
        template.Subcommands.Add(Verb("preview", "Render a template design to a PNG without saving it.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).PreviewTemplate(
                    CliContext.ValueOrFile(p.GetValue(html)!), p.GetValue(css) is { } c ? CliContext.ValueOrFile(c) : null,
                    p.GetValue(sample) is { } s ? CliContext.ParseJsonObject(s) : null, p.GetValue(aspect), ct),
                p.GetValue(output) ?? "template-preview.png"),
            html, css, sample, aspect, output));

        template.Subcommands.Add(Verb("categories", "List the template categories the workspace may use.",
            (p, ct) => CliContext.RunTool(p, api => new TemplateTools(api).ListTemplateCategories(ct))));

        var showId = Arg("template-id", "Template id.");
        template.Subcommands.Add(Verb("show", "Show a template's fields, HTML and CSS.",
            (p, ct) => CliContext.RunTool(p, api => new TemplateTools(api).GetTemplate(p.GetValue(showId)!, ct)),
            showId));

        var editId = Arg("template-id", "Template id.");
        var title = Opt<string?>("--title", "New title.");
        var editHtml = Opt<string?>("--html", "New HTML, inline or @file.");
        var editCss = Opt<string?>("--css", "New CSS, inline or @file.");
        var schema = Opt<string?>("--schema", "Data fields JSON object, inline or @file.");
        template.Subcommands.Add(Verb("edit", "Change a template the workspace owns; slides using it pick up the change.",
            (p, ct) => CliContext.RunTool(p, api => new TemplateTools(api).UpdateTemplate(p.GetValue(editId)!, p.GetValue(title),
                p.GetValue(editHtml) is { } h ? CliContext.ValueOrFile(h) : null, p.GetValue(editCss) is { } c ? CliContext.ValueOrFile(c) : null,
                p.GetValue(schema) is { } s ? CliContext.ParseJsonObject(s) : null, ct)),
            editId, title, editHtml, editCss, schema));

        var deleteId = Arg("template-id", "Template id.");
        template.Subcommands.Add(Verb("delete", "Delete a template; refused while a slide uses it.",
            (p, ct) => CliContext.RunTool(p, api => new TemplateTools(api).DeleteTemplate(p.GetValue(deleteId)!, ct)),
            deleteId));

        var cloneId = Arg("template-id", "Template id, usually a built-in one.");
        template.Subcommands.Add(Verb("clone", "Copy a template into the workspace so it can be changed.",
            (p, ct) => CliContext.RunTool(p, api => new TemplateTools(api).CloneTemplate(p.GetValue(cloneId)!, ct)),
            cloneId));

        var usageId = Arg("template-id", "Template id.");
        template.Subcommands.Add(Verb("usage", "Show which slides use a template.",
            (p, ct) => CliContext.RunTool(p, api => new TemplateTools(api).GetTemplateUsage(p.GetValue(usageId)!, ct)),
            usageId));

        var thumbId = Arg("template-id", "Template id.");
        template.Subcommands.Add(Verb("thumbnail", "Redraw a template's gallery picture.",
            (p, ct) => CliContext.RunTool(p, api => new TemplateTools(api).RegenerateTemplateThumbnail(p.GetValue(thumbId)!, ct)),
            thumbId));
    }

    /// <summary>The <c>platform</c> command: the running release, notices, and help articles, blog and release notes.</summary>
    public static Command Platform()
    {
        var platform = new Command("platform", "InfoSlides itself: the running version, upcoming updates, and help and news articles.");

        platform.Subcommands.Add(Verb("version", "Which InfoSlides release is running, with its release notes.",
            (p, ct) => CliContext.RunTool(p, api => new PlatformTools(api).GetPlatformVersion(ct))));

        platform.Subcommands.Add(Verb("notices", "Active notices, such as an upcoming update and when it starts.",
            (p, ct) => CliContext.RunTool(p, api => new PlatformTools(api).GetPlatformNotices(ct))));

        var q = new Argument<string?>("query") { Description = "Free text; newest first when omitted.", Arity = ArgumentArity.ZeroOrOne };
        var type = Opt<string?>("--type", "help, blog or release-note.");
        var since = Opt<string?>("--since", "Published on or after this date, yyyy-MM-dd.");
        var release = Opt<string?>("--release-version", "Only the notes of this release, e.g. v1.10.2.");
        var page = Opt<int?>("--page", "Page number.");
        platform.Subcommands.Add(Verb("search", "Search the help articles, blog posts and release notes.",
            (p, ct) => CliContext.RunTool(p, api => new PlatformTools(api).SearchArticles(p.GetValue(q), p.GetValue(type),
                p.GetValue(since), p.GetValue(release), p.GetValue(page), null, ct)),
            q, type, since, release, page));

        var articleType = Arg("type", "help, blog or release-note.");
        var slug = Arg("slug", "The slug from `platform search`.");
        platform.Subcommands.Add(Verb("article", "Read one article as Markdown.",
            (p, ct) => CliContext.RunTool(p, api => new PlatformTools(api).GetArticle(p.GetValue(articleType)!, p.GetValue(slug)!, ct)),
            articleType, slug));
        return platform;
    }

    /// <summary>The <c>brand</c> command: read the brand guide, change its draft, and look at sample slides.</summary>
    public static Command Brand()
    {
        var brand = new Command("brand", "The workspace brand guide: colours, fonts, logos and voice (a person confirms it on the Brand page).");

        brand.Subcommands.Add(Verb("show", "Show the guide slides use, the draft, and whether it waits for approval.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).GetBrandGuide(ct))));

        var guide = Arg("guide", "The whole guide as a JSON object, inline or @file.");
        brand.Subcommands.Add(Verb("set", "Replace the draft guide; a person confirms it on the Brand page.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).UpdateBrandGuide(CliContext.ParseJsonObject(p.GetValue(guide)!), ct)),
            guide));

        var sample = Opt<string?>("--sample", "title, text, data, image, logo-light, logo-dark, ticker or clock (default title).");
        var source = Opt<string?>("--source", "draft (default) or confirmed.");
        var output = Opt<string?>("--output", "Output PNG path (default brand-preview.png).");
        brand.Subcommands.Add(Verb("preview", "Draw a sample slide from the brand guide to a PNG.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).PreviewBrand(p.GetValue(sample), p.GetValue(source), ct),
                p.GetValue(output) ?? "brand-preview.png"),
            sample, source, output));

        var checkShow = Arg("slideshow-id", "The slideshow to check.");
        brand.Subcommands.Add(Verb("check", "Check a slideshow against the confirmed brand guide.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).CheckBrand(p.GetValue(checkShow)!, ct)),
            checkShow));

        brand.Subcommands.Add(Verb("samples", "List the sample slides `brand preview` can draw.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).ListBrandPreviews(ct))));

        var logo = Arg("file", "Path to the logo image (up to 5 MB).");
        var slot = Opt<string?>("--slot", "light (default), dark or icon.");
        brand.Subcommands.Add(Verb("logo", "Put a logo into the draft guide; a person confirms it on the Brand page.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).UploadBrandLogo(p.GetValue(logo)!, p.GetValue(slot) ?? "light", ct)),
            logo, slot));
        return brand;
    }

    /// <summary>The <c>takeover</c> command: start, list and end temporary takeovers.</summary>
    public static Command Takeover()
    {
        var takeover = new Command("takeover", "Temporary takeovers: interrupt screens' schedules for a while.");

        var show = Arg("slideshow-id", "The slideshow to show.");
        var devices = Ids("--device", "Screen ids (repeatable or comma-separated).");
        var minutes = Opt<int?>("--minutes", "How long, in minutes.");
        var startsAt = Opt<string?>("--starts-at", "Start (workspace local time, or with Z/offset); default now.");
        var endsAt = Opt<string?>("--ends-at", "End, read like --starts-at.");
        takeover.Subcommands.Add(Verb("create", "Take over screens; without an end it runs until `takeover end`.",
            (p, ct) =>
            {
                if (Split(p.GetValue(devices)) is not { } ids)
                {
                    Console.Error.WriteLine("error: give at least one --device.");
                    return Task.FromResult(2);
                }

                return CliContext.RunTool(p, api => new ScreenTools(api).CreateTakeover(ids, p.GetValue(show)!,
                    p.GetValue(minutes), p.GetValue(startsAt), p.GetValue(endsAt), ct));
            },
            show, devices, minutes, startsAt, endsAt));

        var device = Opt<string?>("--device-id", "Only this screen's takeovers.");
        var active = Opt<bool>("--active", "Only takeovers active now.");
        takeover.Subcommands.Add(Verb("list", "List takeovers.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).ListTakeovers(p.GetValue(device), p.GetValue(active), ct)),
            device, active));

        var id = Arg("takeover-id", "Takeover id.");
        takeover.Subcommands.Add(Verb("end", "End a takeover now.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).EndTakeover(p.GetValue(id)!, ct)),
            id));
        return takeover;
    }

    /// <summary>The <c>team</c> command: list, invite, revoke and remove.</summary>
    public static Command Team()
    {
        var team = new Command("team", "Workspace members and invitations.");
        team.Subcommands.Add(Verb("list", "List members and pending invitations.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).ListTeam(ct))));

        var email = Arg("email", "The person's email address.");
        var role = Opt<string?>("--role", "TenantAdmin (default), ContentManager, DeviceManager or Viewer.");
        team.Subcommands.Add(Verb("invite", "Invite a person to the workspace.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).InviteTeamMember(p.GetValue(email)!, p.GetValue(role), ct)),
            email, role));

        var invitation = Arg("invitation-id", "Invitation id from `team list`.");
        team.Subcommands.Add(Verb("revoke", "Cancel a pending invitation.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).RevokeInvitation(p.GetValue(invitation)!, ct)),
            invitation));

        var member = Arg("member-id", "Member id from `team list`.");
        team.Subcommands.Add(Verb("remove", "Remove a person from the workspace.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).RemoveTeamMember(p.GetValue(member)!, ct)),
            member));

        var deactivate = Arg("member-id", "Member id from `team list`.");
        team.Subcommands.Add(Verb("deactivate", "Turn off a person's access without removing them.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).DeactivateMember(p.GetValue(deactivate)!, ct)),
            deactivate));

        var reactivate = Arg("member-id", "Member id from `team list`.");
        team.Subcommands.Add(Verb("reactivate", "Turn a person's access back on.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).ReactivateMember(p.GetValue(reactivate)!, ct)),
            reactivate));
        return team;
    }

    /// <summary>The <c>workspace</c> command: settings, health and offline alerts.</summary>
    public static Command Workspace()
    {
        var workspace = new Command("workspace", "Workspace settings, health and offline alert emails.");

        var timeZone = Opt<string?>("--time-zone", "IANA time zone, e.g. Europe/London.");
        var locale = Opt<string?>("--locale", "Locale, e.g. en-GB.");
        workspace.Subcommands.Add(Verb("settings", "Set the workspace time zone and/or locale.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).UpdateWorkspaceSettings(p.GetValue(timeZone), p.GetValue(locale), ct)),
            timeZone, locale));

        workspace.Subcommands.Add(Verb("health", "How are my screens: summary, usage and problems with fixes.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).GetWorkspaceHealth(ct))));

        workspace.Subcommands.Add(Verb("offline-alerts", "Show offline alert settings for the workspace and each screen.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).GetOfflineAlerts(ct))));

        var quietStart = Opt<string?>("--quiet-start", "Quiet hours start, HH:mm, e.g. 22:00; give with --quiet-end.");
        var quietEnd = Opt<string?>("--quiet-end", "Quiet hours end, HH:mm, e.g. 07:00.");
        workspace.Subcommands.Add(Verb("set-offline-alerts", "Set the quiet hours for offline emails; neither option alerts at any hour.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).SetWorkspaceOfflineAlerts(p.GetValue(quietStart), p.GetValue(quietEnd), ct)),
            quietStart, quietEnd));

        var entityType = Opt<string?>("--entity-type", "Only events about this kind of thing, e.g. Slideshow.");
        var entityId = Opt<string?>("--entity-id", "Only events about this id.");
        var action = Opt<string?>("--action", "Only this action, e.g. SlideshowDeleted.");
        var (from, to) = (Opt<string?>("--from", "From this time, UTC."), Opt<string?>("--to", "Up to this time, UTC."));
        var page = Opt<int?>("--page", "Page number.");
        workspace.Subcommands.Add(Verb("audit", "Who changed what, newest first (workspace admins).",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).GetAuditTrail(p.GetValue(entityType), p.GetValue(entityId), null,
                p.GetValue(action), null, p.GetValue(from), p.GetValue(to), p.GetValue(page), null, ct)),
            entityType, entityId, action, from, to, page));

        var level = Opt<string?>("--level", "Warning, Error or Critical.");
        var errorPage = Opt<int?>("--page", "Page number.");
        workspace.Subcommands.Add(Verb("errors", "The workspace's error log, newest first (workspace admins).",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).GetErrorLog(p.GetValue(level), null, null, null, null, p.GetValue(errorPage), null, ct)),
            level, errorPage));
        return workspace;
    }

    /// <summary>The <c>ai</c> command: AI Studio slides and the trial.</summary>
    public static Command Ai()
    {
        var ai = new Command("ai", "AI Studio: designed slides from a description, photo, page or document.");

        var show = Arg("slideshow-id", "The slideshow to add the slides to.");
        var handler = Arg("handler", "prompt, photo, url or document.");
        var prompt = Opt<string?>("--prompt", "What the slide should say.");
        var url = Opt<string?>("--url", "Page address (handler url).");
        var mediaAsset = Opt<string?>("--media-asset-id", "Uploaded media id (see `media upload`).");
        var mediaUrl = Opt<string?>("--media-url", "Public address of a photo.");
        var insert = Opt<bool>("--insert", "Add the slides straight away, without a preview step.");
        var branding = Opt<bool?>("--branding", "true features logo and brand colour, false keeps it subtle.");
        var background = Opt<string?>("--background", "auto, none or photo.");
        var backgroundDescription = Opt<string?>("--background-description", "With --background photo: what it shows.");
        ai.Subcommands.Add(Verb("make", "Start an AI slide job; poll it with `ai status`.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).MakeAiSlide(p.GetValue(show)!, p.GetValue(handler)!,
                p.GetValue(prompt), p.GetValue(url), p.GetValue(mediaAsset), p.GetValue(mediaUrl), p.GetValue(insert),
                p.GetValue(branding), p.GetValue(background), p.GetValue(backgroundDescription), ct)),
            show, handler, prompt, url, mediaAsset, mediaUrl, insert, branding, background, backgroundDescription));

        var statusShow = Arg("slideshow-id", "Slideshow id.");
        var statusJob = Arg("job-id", "Job id from `ai make`.");
        ai.Subcommands.Add(Verb("status", "Check an AI slide job (previews once Ready).",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).GetAiSlideJob(p.GetValue(statusShow)!, p.GetValue(statusJob)!, ct)),
            statusShow, statusJob));

        var insertShow = Arg("slideshow-id", "Slideshow id.");
        var insertJob = Arg("job-id", "Id of the Ready job.");
        var position = Opt<int?>("--position", "Zero-based position; appended when omitted.");
        ai.Subcommands.Add(Verb("insert", "Add a Ready job's slides to the slideshow.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).InsertAiSlides(p.GetValue(insertShow)!,
                p.GetValue(insertJob)!, p.GetValue(position), ct)),
            insertShow, insertJob, position));

        ai.Subcommands.Add(Verb("trial", "Start the free AI Studio trial (once per workspace).",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).StartAiStudioTrial(ct))));
        return ai;
    }

    /// <summary>The <c>undo</c> verb.</summary>
    public static Command Undo()
    {
        var token = Arg("token", "The token a change printed (undo with: ...).");
        var force = Opt<bool>("--force", "Overwrite a later change to the same thing.");
        return Verb("undo", "Put back what an earlier change did (within 24 hours).",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).UndoChange(p.GetValue(token)!, p.GetValue(force), ct)),
            token, force);
    }
}
