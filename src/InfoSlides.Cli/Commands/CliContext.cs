using System.CommandLine;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using InfoSlides.Core.Api;
using InfoSlides.Core.Config;
using InfoSlides.Core.Serialization;
using ModelContextProtocol.Protocol;

namespace InfoSlides.Cli.Commands;

/// <summary>Global options, client construction and uniform output/exit-code handling.</summary>
internal static class CliContext
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static readonly Option<string?> ApiUrlOption = new("--api-url")
    {
        Description = "InfoSlides API base URL (overrides INFOSLIDES_API_URL and config).",
        Recursive = true,
    };

    public static readonly Option<string?> ApiKeyOption = new("--api-key")
    {
        Description = "API key or session token (overrides INFOSLIDES_API_KEY and stored credentials).",
        Recursive = true,
    };

    public static readonly Option<bool> JsonOption = new("--json")
    {
        Description = "Emit the raw response envelope as compact JSON on stdout.",
        Recursive = true,
    };

    public static AppSettings Settings(ParseResult parse) =>
        AppSettings.Resolve(parse.GetValue(ApiKeyOption), parse.GetValue(ApiUrlOption));

    public static InfoSlidesApiClient Client(ParseResult parse)
    {
        var settings = Settings(parse);
        return new InfoSlidesApiClient(new HttpClient(), settings.ApiUrl, settings.Credential);
    }

    /// <summary>
    /// Runs an API call and renders the outcome: warnings to stderr, data to stdout (pretty JSON,
    /// or a human summary when provided; the full envelope with --json). Exit code 0/1.
    /// </summary>
    public static async Task<int> Run<T>(
        ParseResult parse,
        Func<InfoSlidesApiClient, Task<ApiResult<T>>> call,
        JsonTypeInfo<T> typeInfo,
        Func<T, string>? human = null)
    {
        try
        {
            var result = await call(Client(parse));
            foreach (var warning in result.Warnings)
            {
                Console.Error.WriteLine($"warning [{warning.Code}]: {warning.Message}");
            }

            if (parse.GetValue(JsonOption))
            {
                var envelope = new JsonObject
                {
                    ["data"] = JsonSerializer.SerializeToNode(result.Data, typeInfo),
                };
                if (result.HasWarnings)
                {
                    envelope["warnings"] = JsonSerializer.SerializeToNode(
                        result.Warnings.ToList(), InfoSlidesJsonContext.Default.ListApiWarning);
                }

                if (result.Undo is { } undo)
                {
                    envelope["undo"] = JsonNode.Parse(undo.GetRawText());
                }

                Console.WriteLine(envelope.ToJsonString());
                return 0;
            }

            if (result.Undo is { } undoRequest &&
                undoRequest.TryGetProperty("body", out var body) && body.TryGetProperty("token", out var token))
            {
                Console.Error.WriteLine($"undo with: infoslides undo {token}");
            }

            if (human is not null)
            {
                Console.WriteLine(human(result.Data));
            }
            else
            {
                Console.WriteLine(
                    JsonSerializer.SerializeToNode(result.Data, typeInfo)?.ToJsonString(Indented) ?? "null");
            }

            return 0;
        }
        catch (Exception exception)
        {
            return Fail(exception);
        }
    }

    /// <summary>
    /// Runs an MCP tool method as a CLI verb, so the CLI and the MCP server share one set of
    /// argument checks and request bodies. Data goes to stdout as pretty JSON (the whole envelope with
    /// --json); warnings, the undo hint and errors, including a NeedsClarification's question and
    /// choices, go to stderr. An image result is saved to <paramref name="pngPath"/>.
    /// </summary>
    /// <param name="parse">The parse result, for the client settings and --json.</param>
    /// <param name="call">Calls the tool with a client built from the settings.</param>
    /// <param name="pngPath">Where an image result is written.</param>
    /// <returns>0 on success, 1 on an error.</returns>
    public static async Task<int> RunTool(
        ParseResult parse, Func<InfoSlidesApiClient, Task<CallToolResult>> call, string pngPath = "image.png")
    {
        CallToolResult result;
        try
        {
            result = await call(Client(parse));
        }
        catch (Exception exception)
        {
            return Fail(exception);
        }

        if (result.Content.OfType<ImageContentBlock>().FirstOrDefault() is { } image)
        {
            // ImageContentBlock.Data holds the base64 text as UTF-8 bytes.
            await File.WriteAllBytesAsync(pngPath, Convert.FromBase64String(Encoding.UTF8.GetString(image.Data.ToArray())));
            Console.WriteLine(Path.GetFullPath(pngPath));
            return 0;
        }

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "{}";
        var node = JsonNode.Parse(text);
        if (result.IsError == true)
        {
            var error = node?["error"];
            Console.Error.WriteLine($"error [{error?["code"]}]: {error?["message"]}");
            if (error?["upgradeUrl"] is { } upgradeUrl)
            {
                Console.Error.WriteLine($"Upgrade at: {upgradeUrl}");
            }

            if (error?["details"] is { } details)
            {
                Console.Error.WriteLine(details.ToJsonString(Indented));
            }

            return 1;
        }

        if (parse.GetValue(JsonOption))
        {
            Console.WriteLine(text);
            return 0;
        }

        foreach (var warning in node?["warnings"]?.AsArray() ?? [])
        {
            Console.Error.WriteLine($"warning [{warning?["code"]}]: {warning?["message"]}");
        }

        if (node?["undo"]?["body"]?["token"] is { } token)
        {
            Console.Error.WriteLine($"undo with: infoslides undo {token}");
        }

        Console.WriteLine(node?["data"]?.ToJsonString(Indented) ?? "null");
        return 0;
    }

    public static int Fail(Exception exception)
    {
        switch (exception)
        {
            case ApiException api:
                Console.Error.WriteLine($"error [{api.Code}]: {api.Message}");
                if (api.UpgradeUrl is { } upgradeUrl)
                {
                    Console.Error.WriteLine($"Upgrade at: {upgradeUrl}");
                }

                return 1;
            case MissingCredentialException:
                Console.Error.WriteLine($"error: {exception.Message}");
                return 1;
            case HttpRequestException:
                Console.Error.WriteLine($"error: could not reach the InfoSlides API: {exception.Message}");
                return 1;
            default:
                throw exception;
        }
    }

    /// <summary>Interprets @path as "read from file", anything else as a literal value.</summary>
    public static string ValueOrFile(string value) =>
        value.StartsWith('@') ? File.ReadAllText(value[1..]) : value;

    public static JsonElement ParseJson(string jsonOrFile)
    {
        using var document = JsonDocument.Parse(ValueOrFile(jsonOrFile));
        return document.RootElement.Clone();
    }

    /// <summary>Parses a JSON object given inline or as <c>@file</c>, for the typed tool parameters.</summary>
    /// <param name="jsonOrFile">Inline JSON, or <c>@path</c> to a file holding it.</param>
    /// <returns>The parsed object.</returns>
    /// <exception cref="InvalidOperationException">The text is not a JSON object.</exception>
    public static System.Text.Json.Nodes.JsonObject ParseJsonObject(string jsonOrFile) =>
        System.Text.Json.Nodes.JsonNode.Parse(ValueOrFile(jsonOrFile)) as System.Text.Json.Nodes.JsonObject
        ?? throw new InvalidOperationException("Expected a JSON object.");
}
