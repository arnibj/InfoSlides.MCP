using System.CommandLine;
using InfoSlides.Core.Config;
using InfoSlides.Core.McpInstall;
using InfoSlides.Core.Models;
using InfoSlides.Core.Serialization;

namespace InfoSlides.Cli.Commands;

internal static class CommandTree
{
    public static RootCommand Build()
    {
        var root = new RootCommand("InfoSlides digital signage CLI. Run with --mcp for the MCP server (stdio).");
        root.Options.Add(CliContext.ApiUrlOption);
        root.Options.Add(CliContext.ApiKeyOption);
        root.Options.Add(CliContext.JsonOption);

        root.Subcommands.Add(LoginCommands.Login());
        root.Subcommands.Add(LoginCommands.Logout());
        root.Subcommands.Add(Tenant());
        root.Subcommands.Add(Verify());
        root.Subcommands.Add(Key());
        root.Subcommands.Add(Slideshow());
        root.Subcommands.Add(Gallery());
        root.Subcommands.Add(Media());
        root.Subcommands.Add(Slide());
        root.Subcommands.Add(Template());
        root.Subcommands.Add(Source());
        root.Subcommands.Add(Device());
        root.Subcommands.Add(Schedule());
        root.Subcommands.Add(Stream());
        root.Subcommands.Add(Billing());
        root.Subcommands.Add(Config());
        root.Subcommands.Add(Mcp());
        return root;
    }

    private static Command Tenant()
    {
        var tenant = new Command("tenant", "Tenant (workspace) operations.");

        var name = new Argument<string>("name") { Description = "Tenant/workspace name." };
        var email = new Argument<string>("owner-email") { Description = "Owner email address." };
        var save = new Option<bool>("--save") { Description = "Store the returned admin API key in ~/.infoslides." };
        var create = new Command("create", "Create a tenant anonymously; returns the Primary Admin API Key.")
        {
            name, email, save,
        };
        create.SetAction((parse, ct) => CliContext.Run(parse,
            async api =>
            {
                // "cli" here, "mcp" from TenantTools — the backend counts agent-originated signups
                // separately from command-line ones.
                var result = await api.CreateTenantAsync(
                    new CreateTenantRequest(parse.GetValue(name)!, parse.GetValue(email)!, "cli"), ct);
                if (parse.GetValue(save))
                {
                    new CredentialStore(CliContext.Settings(parse).ConfigDirectory)
                        .SaveCredentials(new StoredCredentials(
                            ApiKey: result.Data.ApiKey, TenantId: result.Data.TenantId));
                }

                return result;
            },
            InfoSlidesJsonContext.Default.CreateTenantResult,
            data => $"""
                Tenant created: {data.TenantId}
                Admin API key:  {data.ApiKey}
                Store this key securely — it is shown only once.
                A verification email was sent to the owner; most operations unlock after it is confirmed.
                """));
        tenant.Subcommands.Add(create);

        var info = new Command("info", "Show tenant, verification state, subscription and quotas.");
        info.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.GetTenantInfoAsync(ct), InfoSlidesJsonContext.Default.TenantInfo));
        tenant.Subcommands.Add(info);
        return tenant;
    }

    private static Command Verify()
    {
        var verify = new Command("verify", "Email verification helpers.");
        var resend = new Command("resend", "Re-send the owner verification email.");
        resend.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.ResendVerificationEmailAsync(ct), InfoSlidesJsonContext.Default.OkResult,
            _ => "Verification email sent."));
        verify.Subcommands.Add(resend);
        return verify;
    }

    private static Command Key()
    {
        var key = new Command("key", "Tenant API key management.");

        var type = new Argument<string>("type") { Description = "Key type: admin or dataProvider." };
        var name = new Argument<string>("name") { Description = "Display name for the key." };
        var slideIds = new Option<string[]>("--slide-ids")
        {
            Description = "Slide ids a dataProvider key is bound to (repeatable or comma-separated).",
            AllowMultipleArgumentsPerToken = true,
        };
        var create = new Command("create", "Create an API key. dataProvider keys can only push update_source.")
        {
            type, name, slideIds,
        };
        create.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.CreateApiKeyAsync(new CreateApiKeyRequest(
                parse.GetValue(type)!, parse.GetValue(name)!, SplitIds(parse.GetValue(slideIds))), ct),
            InfoSlidesJsonContext.Default.CreateApiKeyResult,
            data => $"""
                Key created: {data.Id} ({data.Type}, {data.Name})
                API key:     {data.Key}
                Store this key securely — it is shown only once.
                """));
        key.Subcommands.Add(create);

        var list = new Command("list", "List API keys (prefixes only).");
        list.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.ListApiKeysAsync(ct), InfoSlidesJsonContext.Default.ListApiKeyInfo));
        key.Subcommands.Add(list);

        var keyId = new Argument<string>("key-id") { Description = "Id of the key to revoke." };
        var revoke = new Command("revoke", "Revoke an API key immediately.") { keyId };
        revoke.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.RevokeApiKeyAsync(parse.GetValue(keyId)!, ct), InfoSlidesJsonContext.Default.OkResult,
            _ => "Key revoked."));
        key.Subcommands.Add(revoke);
        return key;
    }

    private static Command Slideshow()
    {
        var slideshow = new Command("slideshow", "Slideshow (deck) operations.");

        var title = new Argument<string>("title") { Description = "Slideshow title." };
        var width = new Option<int>("--width") { Description = "Horizontal resolution (default 1920).", DefaultValueFactory = _ => 1920 };
        var height = new Option<int>("--height") { Description = "Vertical resolution (default 1080).", DefaultValueFactory = _ => 1080 };
        var upload = new Command("upload", "Create a slideshow.") { title, width, height };
        upload.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.CreateSlideshowAsync(new CreateSlideshowRequest(
                parse.GetValue(title)!, new Resolution(parse.GetValue(width), parse.GetValue(height))), ct),
            InfoSlidesJsonContext.Default.Slideshow));
        slideshow.Subcommands.Add(upload);

        var pptxFile = new Argument<string>("file") { Description = "Path to a .pptx file." };
        var pptxTitle = new Option<string?>("--title") { Description = "Display name (default: the file name)." };
        var uploadPptx = new Command(
            "upload-pptx", "Upload a .pptx file and create a slideshow from it (parses slide count/native " +
                           "resolution and queues thumbnail/stream rendering server-side).")
        {
            pptxFile, pptxTitle,
        };
        uploadPptx.SetAction(async (parse, ct) =>
        {
            var path = parse.GetValue(pptxFile)!;
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"error: file not found: {path}");
                return 2;
            }

            await using var stream = File.OpenRead(path);
            return await CliContext.Run(parse,
                api => api.UploadPptxAsync(stream, Path.GetFileName(path), parse.GetValue(pptxTitle), ct),
                InfoSlidesJsonContext.Default.Slideshow);
        });
        slideshow.Subcommands.Add(uploadPptx);

        var list = new Command("list", "List slideshows.");
        list.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.ListSlideshowsAsync(ct), InfoSlidesJsonContext.Default.ListSlideshow));
        slideshow.Subcommands.Add(list);

        var id = new Argument<string>("slideshow-id") { Description = "Slideshow id." };
        var get = new Command("get", "Show a slideshow with slides and conditions.") { id };
        get.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.GetSlideshowAsync(parse.GetValue(id)!, ct), InfoSlidesJsonContext.Default.Slideshow));
        slideshow.Subcommands.Add(get);

        var updateId = new Argument<string>("slideshow-id") { Description = "Slideshow id." };
        var newTitle = new Option<string?>("--title") { Description = "New title." };
        var newWidth = new Option<int?>("--width") { Description = "New horizontal resolution." };
        var newHeight = new Option<int?>("--height") { Description = "New vertical resolution." };
        var order = new Option<string[]>("--order")
        {
            Description = "Complete slide id order (repeatable or comma-separated).",
            AllowMultipleArgumentsPerToken = true,
        };
        var update = new Command("update", "Update title, resolution or slide order.")
        {
            updateId, newTitle, newWidth, newHeight, order,
        };
        update.SetAction((parse, ct) =>
        {
            var w = parse.GetValue(newWidth);
            var h = parse.GetValue(newHeight);
            if (w.HasValue != h.HasValue)
            {
                Console.Error.WriteLine("error: provide --width and --height together.");
                return Task.FromResult(2);
            }

            return CliContext.Run(parse,
                api => api.UpdateSlideshowAsync(parse.GetValue(updateId)!, new UpdateSlideshowRequest(
                    parse.GetValue(newTitle), w.HasValue ? new Resolution(w.Value, h!.Value) : null,
                    SplitIds(parse.GetValue(order))), ct),
                InfoSlidesJsonContext.Default.Slideshow);
        });
        slideshow.Subcommands.Add(update);

        var sourceId = new Argument<string>("source-id") { Description = "Source slideshow id (or gallery item id with --from-gallery)." };
        var fromGallery = new Option<bool>("--from-gallery") { Description = "Clone from the starter gallery." };
        var clone = new Command("clone", "Clone a slideshow or gallery deck.") { sourceId, fromGallery };
        clone.SetAction((parse, ct) => CliContext.Run(parse,
            api => parse.GetValue(fromGallery)
                ? api.CloneGalleryItemAsync(parse.GetValue(sourceId)!, ct)
                : api.CloneSlideshowAsync(parse.GetValue(sourceId)!, ct),
            InfoSlidesJsonContext.Default.Slideshow));
        slideshow.Subcommands.Add(clone);
        return slideshow;
    }

    private static Command Gallery()
    {
        var gallery = new Command("gallery", "Starter gallery of pre-built decks.");
        var list = new Command("list", "List gallery decks (clone with `slideshow clone --from-gallery`).");
        list.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.ListGalleryAsync(ct), InfoSlidesJsonContext.Default.ListGalleryItem));
        gallery.Subcommands.Add(list);
        return gallery;
    }

    private static Command Media()
    {
        var media = new Command("media", "Media library operations.");

        var file = new Argument<string>("file") { Description = "Path to the file to upload." };
        var upload = new Command(
            "upload", "Upload a file into the tenant's media library. Use the returned id with " +
                      "`slide add-media --asset-id` to add it as a slide.")
        {
            file,
        };
        upload.SetAction(async (parse, ct) =>
        {
            var path = parse.GetValue(file)!;
            if (!System.IO.File.Exists(path))
            {
                Console.Error.WriteLine($"error: file not found: {path}");
                return 2;
            }

            await using var stream = System.IO.File.OpenRead(path);
            return await CliContext.Run(parse,
                api => api.UploadMediaAsync(stream, Path.GetFileName(path), ResolveContentType(path), ct),
                InfoSlidesJsonContext.Default.UploadedMedia);
        });
        media.Subcommands.Add(upload);
        return media;
    }

    /// <summary>Best-effort MIME type from the file extension — advisory only; the server re-derives the
    /// real type from the file's extension/signature.</summary>
    private static string ResolveContentType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".mp4" => "video/mp4",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream",
    };

    private static Command Slide()
    {
        var slide = new Command("slide", "Individual slide operations.");

        var showId = new Argument<string>("slideshow-id") { Description = "Slideshow to add the slide to." };
        var mediaUrl = new Argument<string?>("media-url")
        {
            Description = "URL of the image/video; omit when using --asset-id.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var assetId = new Option<string?>("--asset-id")
        {
            Description = "Id of an existing media library asset (e.g. from `media upload`), instead of media-url.",
        };
        var duration = new Option<double?>("--duration") { Description = "Display duration in seconds." };
        var position = new Option<int?>("--position") { Description = "Zero-based position; appended when omitted." };
        var addMedia = new Command("add-media", "Add a media slide to a slideshow, from a URL or an existing media asset id.")
        {
            showId, mediaUrl, assetId, duration, position,
        };
        addMedia.SetAction((parse, ct) =>
        {
            var url = parse.GetValue(mediaUrl);
            var asset = parse.GetValue(assetId);
            if ((url is null) == (asset is null))
            {
                Console.Error.WriteLine("error: provide exactly one of media-url or --asset-id.");
                return Task.FromResult(2);
            }

            return CliContext.Run(parse,
                api => api.AddMediaSlideAsync(parse.GetValue(showId)!, new AddMediaSlideRequest(
                    url, asset, parse.GetValue(duration), parse.GetValue(position)), ct),
                InfoSlidesJsonContext.Default.Slide);
        });
        slide.Subcommands.Add(addMedia);

        var dynShowId = new Argument<string>("slideshow-id") { Description = "Slideshow to add the slide to." };
        var templateId = new Argument<string>("template-id") { Description = "Id of a template (from `template create`/`template list`)." };
        var dynDuration = new Option<double?>("--duration") { Description = "Display duration in seconds." };
        var dynPosition = new Option<int?>("--position") { Description = "Zero-based position; appended when omitted." };
        var addDynamic = new Command(
            "add-dynamic",
            "Add a template-driven dynamic slide to a slideshow. Starts with no data — push it " +
            "with `source update`.")
        {
            dynShowId, templateId, dynDuration, dynPosition,
        };
        addDynamic.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.AddDynamicSlideAsync(parse.GetValue(dynShowId)!, new AddDynamicSlideRequest(
                parse.GetValue(templateId)!, parse.GetValue(dynDuration), parse.GetValue(dynPosition)), ct),
            InfoSlidesJsonContext.Default.Slide));
        slide.Subcommands.Add(addDynamic);

        var slideId = new Argument<string>("slide-id") { Description = "Slide id." };
        var condition = new Option<string[]>("--condition")
        {
            Description = "Visibility condition as type=value; repeatable. Types: time ('08:00-11:00'), " +
                          "weekday ('sat,sun'), data_trigger ('sales_today > 1000000'). No conditions clears all.",
        };
        var setConditions = new Command("set-conditions", "Replace a slide's visibility conditions.")
        {
            slideId, condition,
        };
        setConditions.SetAction((parse, ct) =>
        {
            var conditions = new List<SlideCondition>();
            foreach (var raw in parse.GetValue(condition) ?? [])
            {
                var split = raw.Split('=', 2);
                if (split.Length != 2 || split[0] is not ("time" or "weekday" or "data_trigger"))
                {
                    Console.Error.WriteLine($"error: invalid condition '{raw}'; expected time=…, weekday=… or data_trigger=….");
                    return Task.FromResult(2);
                }

                conditions.Add(new SlideCondition(split[0], split[1]));
            }

            return CliContext.Run(parse,
                api => api.SetSlideConditionsAsync(parse.GetValue(slideId)!, conditions, ct),
                InfoSlidesJsonContext.Default.Slide);
        });
        slide.Subcommands.Add(setConditions);

        var previewId = new Argument<string>("slide-id") { Description = "Slide id to render." };
        var output = new Option<string?>("--output") { Description = "Output PNG path (default slide-<id>.png)." };
        var preview = new Command("preview", "Render a slide to a PNG for visual verification.")
        {
            previewId, output,
        };
        preview.SetAction(async (parse, ct) =>
        {
            try
            {
                var pngBytes = await CliContext.Client(parse).GetSlidePreviewPngAsync(parse.GetValue(previewId)!, ct);
                var path = parse.GetValue(output) ?? $"slide-{parse.GetValue(previewId)}.png";
                await File.WriteAllBytesAsync(path, pngBytes, ct);
                Console.WriteLine(Path.GetFullPath(path));
                return 0;
            }
            catch (Exception exception)
            {
                return CliContext.Fail(exception);
            }
        });
        slide.Subcommands.Add(preview);
        return slide;
    }

    private static Command Template()
    {
        var template = new Command("template", "Dynamic slide templates (Premium).");

        var title = new Argument<string>("title") { Description = "Template title." };
        var prompt = new Option<string?>("--prompt") { Description = "AI Studio prompt (AI mode; requires --sample-json)." };
        var sampleJson = new Option<string?>("--sample-json") { Description = "Sample JSON schema, inline or @file (AI mode)." };
        var html = new Option<string?>("--html") { Description = "Raw HTML with {{field}} placeholders, inline or @file (code mode)." };
        var css = new Option<string?>("--css") { Description = "Stylesheet, inline or @file (code mode)." };
        var dryRun = new Option<bool>("--dry-run") { Description = "Validate without creating." };
        var create = new Command("create", "Create a template from an AI prompt or raw HTML/CSS.")
        {
            title, prompt, sampleJson, html, css, dryRun,
        };
        create.SetAction((parse, ct) =>
        {
            var promptValue = parse.GetValue(prompt);
            var htmlValue = parse.GetValue(html);
            if ((promptValue is null) == (htmlValue is null))
            {
                Console.Error.WriteLine("error: provide exactly one of --prompt (+ --sample-json) or --html (+ optional --css).");
                return Task.FromResult(2);
            }

            if (promptValue is not null && parse.GetValue(sampleJson) is null)
            {
                Console.Error.WriteLine("error: --sample-json is required with --prompt.");
                return Task.FromResult(2);
            }

            return CliContext.Run(parse,
                api => api.CreateTemplateAsync(new CreateTemplateRequest(
                        parse.GetValue(title)!,
                        promptValue,
                        parse.GetValue(sampleJson) is { } sj ? CliContext.ParseJson(sj) : null,
                        htmlValue is { } hv ? CliContext.ValueOrFile(hv) : null,
                        parse.GetValue(css) is { } cv ? CliContext.ValueOrFile(cv) : null),
                    parse.GetValue(dryRun), ct),
                InfoSlidesJsonContext.Default.Template);
        });
        template.Subcommands.Add(create);

        var list = new Command("list", "List templates with their data schemas.");
        list.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.ListTemplatesAsync(ct), InfoSlidesJsonContext.Default.ListTemplate));
        template.Subcommands.Add(list);
        return template;
    }

    private static Command Source()
    {
        var source = new Command("source", "Push data to template-based slides.");

        var slideId = new Argument<string>("slide-id") { Description = "Slide id." };
        var data = new Option<string>("--data") { Description = "JSON payload, inline or @file.", Required = true };
        var dryRun = new Option<bool>("--dry-run") { Description = "Validate against the template schema without rendering." };
        var update = new Command("update", "Push a JSON payload and trigger a re-render (data-provider keys allowed).")
        {
            slideId, data, dryRun,
        };
        update.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.UpdateSourceAsync(parse.GetValue(slideId)!,
                CliContext.ParseJson(parse.GetValue(data)!), parse.GetValue(dryRun), ct),
            InfoSlidesJsonContext.Default.OkResult,
            _ => parse.GetValue(dryRun) ? "Payload is valid." : "Source updated; slide re-render triggered."));
        source.Subcommands.Add(update);
        return source;
    }

    private static Command Device()
    {
        var device = new Command("device", "Display device (screen) operations.");

        var name = new Argument<string>("name") { Description = "Device name, e.g. 'Lobby screen'." };
        var width = new Option<int>("--width") { Description = "Horizontal resolution (default 1920).", DefaultValueFactory = _ => 1920 };
        var height = new Option<int>("--height") { Description = "Vertical resolution (default 1080).", DefaultValueFactory = _ => 1080 };
        var create = new Command("create", "Register a device (free tier: max 1 active).") { name, width, height };
        create.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.CreateDeviceAsync(new CreateDeviceRequest(
                parse.GetValue(name)!, new Resolution(parse.GetValue(width), parse.GetValue(height))), ct),
            InfoSlidesJsonContext.Default.Device));
        device.Subcommands.Add(create);

        var list = new Command("list", "List devices.");
        list.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.ListDevicesAsync(ct), InfoSlidesJsonContext.Default.ListDevice));
        device.Subcommands.Add(list);

        var id = new Argument<string>("device-id") { Description = "Device id." };
        var status = new Command("status", "Show online state, last-seen and what's playing.") { id };
        status.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.GetDeviceStatusAsync(parse.GetValue(id)!, ct), InfoSlidesJsonContext.Default.DeviceStatus));
        device.Subcommands.Add(status);
        return device;
    }

    private static Command Schedule()
    {
        var schedule = new Command("schedule", "Assign content to devices.");

        var deviceId = new Argument<string>("device-id") { Description = "Device id." };
        var slideshowIds = new Argument<string[]>("slideshow-ids") { Description = "Slideshow ids to play, in order." };
        var assign = new Command("assign", "Assign slideshow(s) to a device (warns on aspect-ratio mismatch).")
        {
            deviceId, slideshowIds,
        };
        assign.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.AssignScheduleAsync(parse.GetValue(deviceId)!,
                new AssignScheduleRequest(parse.GetValue(slideshowIds)!), ct),
            InfoSlidesJsonContext.Default.Schedule));
        schedule.Subcommands.Add(assign);
        return schedule;
    }

    private static Command Stream()
    {
        var stream = new Command("stream", "Live HLS streams.");
        var deviceId = new Argument<string>("device-id") { Description = "Device id." };
        var link = new Command("link", "Get the live HLS stream URL for a device.") { deviceId };
        link.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.GetStreamLinkAsync(parse.GetValue(deviceId)!, ct),
            InfoSlidesJsonContext.Default.StreamLink,
            data => data.HlsUrl));
        stream.Subcommands.Add(link);
        return stream;
    }

    private static Command Billing()
    {
        var billing = new Command("billing", "Subscription and billing.");
        var upgrade = new Command("upgrade", "Get a Paddle checkout link to upgrade to Premium.");
        upgrade.SetAction((parse, ct) => CliContext.Run(parse,
            api => api.CreateCheckoutAsync(ct), InfoSlidesJsonContext.Default.CheckoutLink,
            data => $"Open this link to upgrade: {data.CheckoutUrl}"));
        billing.Subcommands.Add(upgrade);
        return billing;
    }

    private static Command Config()
    {
        var config = new Command("config", "Local CLI configuration (~/.infoslides).");

        var get = new Command("get", "Show resolved settings and credential source.");
        get.SetAction(parse =>
        {
            var settings = CliContext.Settings(parse);
            Console.WriteLine($"apiUrl:     {settings.ApiUrl}");
            Console.WriteLine($"credential: {(settings.Credential is null ? "(none)" : Redact(settings.Credential))}");
            Console.WriteLine($"configDir:  {settings.ConfigDirectory}");
            return 0;
        });
        config.Subcommands.Add(get);

        var apiUrl = new Option<string?>("--api-url") { Description = "Persist a default API base URL." };
        var set = new Command("set", "Persist configuration values.") { apiUrl };
        set.SetAction(parse =>
        {
            if (parse.GetValue(apiUrl) is not { } url)
            {
                Console.Error.WriteLine("error: nothing to set; pass --api-url.");
                return 2;
            }

            var store = new CredentialStore(CliContext.Settings(parse).ConfigDirectory);
            store.SaveConfig(new StoredConfig(url));
            Console.WriteLine($"apiUrl saved to {store.ConfigPath}");
            return 0;
        });
        config.Subcommands.Add(set);
        return config;
    }

    private static Command Mcp()
    {
        var mcp = new Command("mcp", "MCP server helpers (run the server itself with `infoslides --mcp`).");

        var client = new Option<string>("--client")
        {
            Description = "MCP client to configure: claude-code, claude-desktop or cursor.",
            Required = true,
        };
        var configPath = new Option<string?>("--config-path") { Description = "Override the client config file path." };
        var install = new Command("install", "Add the infoslides MCP server to a client's configuration (merges, keeps a .bak).")
        {
            client, configPath,
        };
        install.SetAction(parse =>
        {
            var clientName = parse.GetValue(client)!;
            if (!McpClientConfigWriter.SupportedClients.Contains(clientName))
            {
                Console.Error.WriteLine(
                    $"error: unknown client '{clientName}'. Supported: {string.Join(", ", McpClientConfigWriter.SupportedClients)}.");
                return 2;
            }

            var settings = CliContext.Settings(parse);
            var (command, args) = McpClientConfigWriter.ResolveLaunch(
                Environment.ProcessPath, AppContext.BaseDirectory);
            var result = McpClientConfigWriter.Install(
                clientName,
                command,
                args,
                settings.Credential,
                settings.ApiUrl.ToString() == AppSettings.DefaultApiUrl ? null : settings.ApiUrl.ToString().TrimEnd('/'),
                parse.GetValue(configPath));
            Console.WriteLine($"MCP server 'infoslides' written to {result.ConfigPath}");
            if (result.BackupPath is not null)
            {
                Console.WriteLine($"Previous config backed up to {result.BackupPath}");
            }

            if (settings.Credential is null)
            {
                Console.WriteLine("No credential configured yet — the server will only be able to call create_tenant. " +
                                  "Run `infoslides login` or `infoslides tenant create --save` first, then re-run this command.");
            }

            return 0;
        });
        mcp.Subcommands.Add(install);
        return mcp;
    }

    private static string Redact(string credential) =>
        credential.Length <= 12 ? "****" : credential[..12] + "…";

    private static List<string>? SplitIds(string[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return null;
        }

        var ids = values.SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToList();
        return ids.Count == 0 ? null : ids;
    }
}
