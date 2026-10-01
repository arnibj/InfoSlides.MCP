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
    }

    /// <summary>Adds play time per screen to <c>slideshow</c>.</summary>
    /// <param name="slideshow">The <c>slideshow</c> command.</param>
    public static void AddSlideshowVerbs(Command slideshow)
    {
        var id = Arg("slideshow-id", "Slideshow id.");
        var (from, to) = (Opt<string?>("--from", "Start date, yyyy-MM-dd."), Opt<string?>("--to", "End date, yyyy-MM-dd."));
        slideshow.Subcommands.Add(Verb("plays", "Estimated play time per screen for a slideshow.",
            (p, ct) => CliContext.RunTool(p, api => new ScreenTools(api).GetSlideshowPlays(p.GetValue(id)!, p.GetValue(from)!, p.GetValue(to)!, ct)),
            id, from, to));
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
                p.GetValue(config) is { } c ? CliContext.ParseJson(c) : null, p.GetValue(interval), ct)),
            adapter, name, config, interval));

        var editId = Arg("source-id", "Source id.");
        var editName = Opt<string?>("--name", "New display name.");
        var editConfig = Opt<string?>("--config", "New config JSON object, inline or @file.");
        var editInterval = Opt<int?>("--interval", "New fetch interval in seconds.");
        var enabled = Opt<bool?>("--enabled", "false pauses fetching, true resumes it.");
        source.Subcommands.Add(Verb("edit", "Change a source's name, config or interval, or pause and resume it.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).EditSource(p.GetValue(editId)!, p.GetValue(editName),
                p.GetValue(editConfig) is { } c ? CliContext.ParseJson(c) : null, p.GetValue(editInterval), p.GetValue(enabled), ct)),
            editId, editName, editConfig, editInterval, enabled));

        var deleteId = Arg("source-id", "Source id.");
        source.Subcommands.Add(Verb("delete", "Delete a source; slides and tickers fed by it stop updating.",
            (p, ct) => CliContext.RunTool(p, api => new WorkspaceTools(api).DeleteSource(p.GetValue(deleteId)!, ct)),
            deleteId));
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
                    p.GetValue(sample) is { } s ? CliContext.ParseJson(s) : null, p.GetValue(aspect), ct),
                p.GetValue(output) ?? "template-preview.png"),
            html, css, sample, aspect, output));
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
