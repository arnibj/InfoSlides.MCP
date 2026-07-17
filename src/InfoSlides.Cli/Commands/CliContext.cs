using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using InfoSlides.Core.Api;
using InfoSlides.Core.Config;
using InfoSlides.Core.Serialization;

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

                Console.WriteLine(envelope.ToJsonString());
            }
            else if (human is not null)
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
}
