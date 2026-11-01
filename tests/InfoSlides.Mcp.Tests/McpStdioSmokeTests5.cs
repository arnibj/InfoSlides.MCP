using System.Text.Json;
using InfoSlides.Mcp.Tools;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>Stdio smoke tests, part 5. See <see cref="McpStdioSmokeBase"/> for why they are split.</summary>
public sealed class McpStdioSmokeTests5 : McpStdioSmokeBase
{
    [Fact]
    public async Task Cli_AtFileOption_ReadsTheFile_InsteadOfExpandingItAsAResponseFile()
    {
        // System.CommandLine treats a leading @ as a response file by default and splices the
        // file's words into the command line, so `--html @page.html` failed with "Unrecognized
        // command or argument" before the command ever ran.
        _backend.MapJson("POST", "/v1/templates", """{"data":{"id":"t1","title":"Queue"}}""", status: 201);
        var html = Path.Combine(_isolatedHome, "page.html");
        await File.WriteAllTextAsync(html, "<div class=\"board\">{{nowServing}} waiting</div>");

        var psi = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "infoslides.dll"), "template", "create", "Queue", "--html", "@" + html })
        {
            psi.ArgumentList.Add(arg);
        }

        psi.Environment["INFOSLIDES_API_URL"] = _backend.BaseUrl.ToString().TrimEnd('/');
        psi.Environment["INFOSLIDES_API_KEY"] = "isk_admin_test";
        psi.Environment["HOME"] = _isolatedHome;
        psi.Environment["USERPROFILE"] = _isolatedHome;
        using var process = System.Diagnostics.Process.Start(psi)!;
        var stderr = await process.StandardError.ReadToEndAsync(Timeout());
        await process.WaitForExitAsync(Timeout());

        Assert.True(process.ExitCode == 0, stderr);
        lock (_backend.Requests)
        {
            Assert.Contains("{{nowServing}} waiting", Assert.Single(_backend.Requests).Body);
        }
    }

    /// <summary>
    /// Nullable bool options (<c>--hidden</c>, <c>--ticker</c>) must accept an explicit false as well
    /// as a bare flag, or "show the slide again" and "turn the ticker off" cannot be said.
    /// </summary>
    [Theory]
    [InlineData(new[] { "slide", "update", "sl1", "--hidden", "false" }, "PATCH", "/v1/slides/sl1", "{\"hidden\":false}")]
    [InlineData(new[] { "slide", "update", "sl1", "--hidden" }, "PATCH", "/v1/slides/sl1", "{\"hidden\":true}")]
    [InlineData(new[] { "slideshow", "update", "s1", "--ticker", "false", "--default-duration", "15" }, "PATCH", "/v1/slideshows/s1",
        "{\"defaultDurationSeconds\":15,\"ticker\":{\"enabled\":false}}")]
    [InlineData(new[] { "slide", "set-conditions", "sl1", "--condition", "date=2026-12-01..2026-12-26", "--mode", "hide", "--match", "any" },
        "PUT", "/v1/slides/sl1/conditions",
        "{\"conditions\":[{\"type\":\"date\",\"value\":\"2026-12-01..2026-12-26\"}],\"mode\":\"hide\",\"match\":\"any\"}")]
    [InlineData(new[] { "slide", "delete", "sl1" }, "DELETE", "/v1/slides/sl1", "")]
    [InlineData(new[] { "slideshow", "delete", "s1" }, "DELETE", "/v1/slideshows/s1", "")]
    [InlineData(new[] { "source", "list" }, "GET", "/v1/sources", "")]
    [InlineData(new[] { "schedule", "show", "d1" }, "GET", "/v1/devices/d1/schedule", "")]
    [InlineData(new[] { "schedule", "add", "d1", "s1", "11:00", "14:00" }, "POST", "/v1/devices/d1/schedule/entries",
        "{\"slideshowId\":\"s1\",\"startTime\":\"11:00\",\"endTime\":\"14:00\",\"priority\":0}")]
    [InlineData(new[] { "schedule", "remove", "d1", "e1" }, "DELETE", "/v1/devices/d1/schedule/entries/e1", "")]
    [InlineData(new[] { "source", "adapters" }, "GET", "/v1/adapters", "")]
    [InlineData(new[] { "source", "edit", "src1", "--enabled", "false" }, "PATCH", "/v1/sources/src1", "{\"isEnabled\":false}")]
    [InlineData(new[] { "source", "delete", "src1" }, "DELETE", "/v1/sources/src1", "")]
    [InlineData(new[] { "source", "fetch", "src1" }, "POST", "/v1/sources/src1/fetch", "")]
    [InlineData(new[] { "source", "items", "src1", "--limit", "5" }, "GET", "/v1/sources/src1/items", "")]
    [InlineData(new[] { "source", "test", "RssFeed", "--config", "{\"feedUrl\":\"https://x.test/rss\"}" }, "POST", "/v1/sources/test-connection",
        "{\"adapterType\":\"RssFeed\",\"config\":{\"feedUrl\":\"https://x.test/rss\"}}")]
    [InlineData(new[] { "platform", "version" }, "GET", "/v1/platform/version", "")]
    [InlineData(new[] { "platform", "article", "help", "pairing" }, "GET", "/v1/articles/help/pairing", "")]
    [InlineData(new[] { "media", "edit", "m1", "--root" }, "PATCH", "/v1/media/m1", "{\"folderId\":null}")]
    [InlineData(new[] { "media", "folder-create", "Menus" }, "POST", "/v1/media/folders", "{\"name\":\"Menus\"}")]
    [InlineData(new[] { "template", "clone", "t1" }, "POST", "/v1/templates/t1/clone", "")]
    [InlineData(new[] { "schedule", "edit", "d1", "e1", "--end", "15:00" }, "PATCH", "/v1/devices/d1/schedule/entries/e1", "{\"endTime\":\"15:00\"}")]
    [InlineData(new[] { "slideshow", "import-slide", "s1", "s2", "3" }, "POST", "/v1/slideshows/s1/slides/import",
        "{\"sourceSlideshowId\":\"s2\",\"slideNumber\":3}")]
    [InlineData(new[] { "team", "deactivate", "u1" }, "POST", "/v1/team/members/u1/deactivate", "")]
    [InlineData(new[] { "billing", "show" }, "GET", "/v1/billing", "")]
    public async Task Cli_NewEditVerbs_SendTheExpectedRequest(string[] args, string method, string path, string body)
    {
        _backend.MapJson(method, path, """{"data":{"id":"x","title":"t","resolution":{"width":1,"height":1}}}""");
        if (method == "GET")
        {
            _backend.MapJson(method, path, """{"data":[]}""");
        }

        var psi = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "infoslides.dll"));
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        psi.Environment["INFOSLIDES_API_URL"] = _backend.BaseUrl.ToString().TrimEnd('/');
        psi.Environment["INFOSLIDES_API_KEY"] = "isk_admin_test";
        psi.Environment["HOME"] = _isolatedHome;
        psi.Environment["USERPROFILE"] = _isolatedHome;
        using var process = System.Diagnostics.Process.Start(psi)!;
        var stderr = await process.StandardError.ReadToEndAsync(Timeout());
        await process.WaitForExitAsync(Timeout());

        Assert.True(process.ExitCode == 0, stderr);
        lock (_backend.Requests)
        {
            var request = Assert.Single(_backend.Requests);
            Assert.Equal(method, request.Method);
            Assert.Equal(path, request.Path);
            Assert.Equal(body, request.Body);
        }
    }
}
