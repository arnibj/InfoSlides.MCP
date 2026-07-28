using System.Net;
using System.Text.Json;
using InfoSlides.Core.Serialization;
using InfoSlides.Core.Update;
using Xunit;

namespace InfoSlides.Core.Tests;

/// <summary>
/// Tests for the release-update notice. The check must be invisible when it has nothing to say
/// and harmless when it fails — an update nag that breaks a command is worse than no nag.
/// </summary>
public sealed class UpdateCheckerTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "infoslides-update-tests-" + Guid.NewGuid().ToString("N"));

    // The real environment may set CI, which disables the checker; tests supply their own lookup
    // so they behave the same on a dev machine and on a build agent.
    private static string? NoEnv(string _) => null;

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private void WriteCache(string? latest, DateTimeOffset checkedAt)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(
            UpdateChecker.CachePath(_dir),
            JsonSerializer.Serialize(
                new UpdateCheckState(latest, checkedAt), InfoSlidesJsonContext.Default.UpdateCheckState));
    }

    // ── Version comparison ───────────────────────────────────────────────────

    [Theory]
    [InlineData("1.2.0", "1.1.0", true)]
    [InlineData("v1.2.0", "1.1.0", true)]      // tolerates the tag's leading v
    [InlineData("1.1.1", "1.1.0", true)]
    [InlineData("1.1.0", "1.1.0", false)]      // equal is not newer
    [InlineData("1.0.0", "1.1.0", false)]      // older never nags
    [InlineData("v1.10.0", "v1.9.0", true)]    // numeric, not lexicographic
    [InlineData(null, "1.1.0", false)]
    [InlineData("garbage", "1.1.0", false)]
    [InlineData("1.2.0", "garbage", false)]
    public void IsNewer_ComparesNumerically_AndToleratesJunk(string? candidate, string? current, bool expected)
    {
        Assert.Equal(expected, UpdateChecker.IsNewer(candidate, current));
    }

    // ── The notice ───────────────────────────────────────────────────────────

    [Fact]
    public void GetPendingNotice_NoCache_ReturnsNull()
    {
        Assert.Null(UpdateChecker.GetPendingNotice("1.1.0", _dir, NoEnv));
    }

    [Fact]
    public void GetPendingNotice_NewerCached_MentionsBothVersionsAndTheReleasesPage()
    {
        WriteCache("1.2.0", DateTimeOffset.UtcNow);

        var notice = UpdateChecker.GetPendingNotice("1.1.0", _dir, NoEnv);

        Assert.NotNull(notice);
        Assert.Contains("1.1.0", notice);
        Assert.Contains("1.2.0", notice);
        Assert.Contains("github.com/arnibj/InfoSlides.MCP/releases", notice);
    }

    [Fact]
    public void GetPendingNotice_UpToDate_ReturnsNull()
    {
        WriteCache("1.1.0", DateTimeOffset.UtcNow);

        Assert.Null(UpdateChecker.GetPendingNotice("1.1.0", _dir, NoEnv));
    }

    [Fact]
    public void GetPendingNotice_StaleCacheStillNotifies()
    {
        // Staleness governs whether to re-fetch, not whether the last known answer is usable —
        // an offline user should still be told about the update they were told about yesterday.
        WriteCache("1.2.0", DateTimeOffset.UtcNow - TimeSpan.FromDays(30));

        Assert.NotNull(UpdateChecker.GetPendingNotice("1.1.0", _dir, NoEnv));
    }

    [Fact]
    public void GetPendingNotice_CorruptCache_ReturnsNullInsteadOfThrowing()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(UpdateChecker.CachePath(_dir), "{ this is not json");

        Assert.Null(UpdateChecker.GetPendingNotice("1.1.0", _dir, NoEnv));
    }

    // ── Opt-out ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(UpdateChecker.DisableEnvVar)]
    [InlineData("CI")]
    public void GetPendingNotice_Disabled_ReturnsNullEvenWithANewerVersionCached(string variable)
    {
        WriteCache("1.2.0", DateTimeOffset.UtcNow);

        var notice = UpdateChecker.GetPendingNotice(
            "1.1.0", _dir, name => name == variable ? "1" : null);

        Assert.Null(notice);
    }

    [Fact]
    public async Task RefreshAsync_Disabled_MakesNoRequest()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, """{"tag_name":"v9.9.9"}""");

        await UpdateChecker.RefreshAsync(
            _dir, new HttpClient(handler), name => name == UpdateChecker.DisableEnvVar ? "1" : null);

        Assert.Equal(0, handler.Calls);
        Assert.False(File.Exists(UpdateChecker.CachePath(_dir)));
    }

    // ── Refresh ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_StoresTheLatestTagWithoutItsLeadingV()
    {
        var handler = new CountingHandler(HttpStatusCode.OK, """{"tag_name":"v1.2.0"}""");

        await UpdateChecker.RefreshAsync(_dir, new HttpClient(handler), NoEnv);

        var state = JsonSerializer.Deserialize(
            File.ReadAllText(UpdateChecker.CachePath(_dir)),
            InfoSlidesJsonContext.Default.UpdateCheckState);

        Assert.Equal("1.2.0", state!.LatestVersion);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task RefreshAsync_FreshCache_SkipsTheNetwork()
    {
        WriteCache("1.2.0", DateTimeOffset.UtcNow);
        var handler = new CountingHandler(HttpStatusCode.OK, """{"tag_name":"v9.9.9"}""");

        await UpdateChecker.RefreshAsync(_dir, new HttpClient(handler), NoEnv);

        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task RefreshAsync_StaleCache_RefetchesAndOverwrites()
    {
        WriteCache("1.1.0", DateTimeOffset.UtcNow - TimeSpan.FromDays(2));
        var handler = new CountingHandler(HttpStatusCode.OK, """{"tag_name":"v1.3.0"}""");

        await UpdateChecker.RefreshAsync(_dir, new HttpClient(handler), NoEnv);

        var state = JsonSerializer.Deserialize(
            File.ReadAllText(UpdateChecker.CachePath(_dir)),
            InfoSlidesJsonContext.Default.UpdateCheckState);

        Assert.Equal("1.3.0", state!.LatestVersion);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task RefreshAsync_SendsAUserAgent()
    {
        // GitHub rejects requests without one, so this would otherwise fail silently forever.
        var handler = new CountingHandler(HttpStatusCode.OK, """{"tag_name":"v1.2.0"}""");

        await UpdateChecker.RefreshAsync(_dir, new HttpClient(handler), NoEnv);

        Assert.NotNull(handler.LastUserAgent);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, """{"message":"rate limit exceeded"}""")]
    [InlineData(HttpStatusCode.NotFound, """{"message":"Not Found"}""")]
    [InlineData(HttpStatusCode.OK, "not json at all")]
    [InlineData(HttpStatusCode.OK, """{"tag_name":null}""")]
    public async Task RefreshAsync_BadResponse_WritesNothingAndDoesNotThrow(
        HttpStatusCode status, string body)
    {
        await UpdateChecker.RefreshAsync(_dir, new HttpClient(new CountingHandler(status, body)), NoEnv);

        Assert.False(File.Exists(UpdateChecker.CachePath(_dir)));
    }

    [Fact]
    public async Task RefreshAsync_NetworkFailure_DoesNotThrow()
    {
        // The whole point: an offline machine must not see an error from a background nicety.
        await UpdateChecker.RefreshAsync(_dir, new HttpClient(new ThrowingHandler()), NoEnv);

        Assert.False(File.Exists(UpdateChecker.CachePath(_dir)));
    }

    private sealed class CountingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string? LastUserAgent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUserAgent = request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("no network");
    }
}
