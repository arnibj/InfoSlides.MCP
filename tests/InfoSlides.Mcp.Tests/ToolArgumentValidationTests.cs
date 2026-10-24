// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Text.Json;
using InfoSlides.Mcp.Tools;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>
/// Tool arguments are checked against the tool's input schema before the tool runs: a missing required argument
/// or one the tool does not take is named, the same way the REST API names an unknown body field.
/// </summary>
public class ToolArgumentValidationTests
{
    private static readonly JsonElement Schema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "deviceId": { "type": "string", "description": "The screen." },
            "from": { "type": "string", "description": "Start date, yyyy-MM-dd." },
            "note": { "type": ["string", "null"] }
          },
          "required": ["deviceId", "from"]
        }
        """).RootElement;

    private static Dictionary<string, JsonElement> Args(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact]
    public void AllRequiredPresent_IsValid()
    {
        Assert.Null(ToolArgumentValidation.Problem(Schema, Args("""{"deviceId":"d","from":"2026-10-01"}""")));
    }

    [Fact]
    public void OptionalArgumentAsNull_IsValid()
    {
        Assert.Null(ToolArgumentValidation.Problem(Schema, Args("""{"deviceId":"d","from":"2026-10-01","note":null}""")));
    }

    [Fact]
    public void MissingRequired_NamesItAndSaysWhatItIs()
    {
        var problem = ToolArgumentValidation.Problem(Schema, Args("""{"deviceId":"d"}"""));

        Assert.NotNull(problem);
        Assert.Contains("'from'", problem);
        Assert.Contains("Start date", problem);
    }

    [Fact]
    public void RequiredSentAsNull_CountsAsMissing()
    {
        var problem = ToolArgumentValidation.Problem(Schema, Args("""{"deviceId":"d","from":null}"""));

        Assert.NotNull(problem);
        Assert.Contains("'from'", problem);
    }

    [Fact]
    public void NoArgumentsAtAll_NamesEveryMissingRequired()
    {
        var problem = ToolArgumentValidation.Problem(Schema, null);

        Assert.NotNull(problem);
        Assert.Contains("'deviceId'", problem);
        Assert.Contains("'from'", problem);
    }

    [Fact]
    public void UnknownArgument_NamesItAndListsTheOnesTheToolTakes()
    {
        var problem = ToolArgumentValidation.Problem(Schema, Args("""{"deviceId":"d","from":"x","playbackModeOverride":"Html"}"""));

        Assert.NotNull(problem);
        Assert.Contains("'playbackModeOverride'", problem);
        Assert.Contains("deviceId, from, note", problem);
    }
}
