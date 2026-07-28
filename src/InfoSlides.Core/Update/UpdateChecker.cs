using System.Text.Json;
using System.Text.Json.Serialization;
using InfoSlides.Core.Serialization;

namespace InfoSlides.Core.Update;

/// <summary>Cached result of the last release check, stored in the config directory.</summary>
/// <param name="LatestVersion">Latest version seen on GitHub, without the leading "v".</param>
/// <param name="CheckedAt">When the check ran, so staleness can be judged.</param>
public sealed record UpdateCheckState(string? LatestVersion, DateTimeOffset CheckedAt);

/// <summary>The subset of GitHub's release payload this needs.</summary>
/// <param name="TagName">Release tag, e.g. <c>v1.2.0</c>.</param>
public sealed record GitHubRelease([property: JsonPropertyName("tag_name")] string? TagName);

/// <summary>
/// Tells users a newer release exists. No install route notifies on its own — a sideloaded
/// <c>.mcpb</c> has no update channel, archives are plain downloads, and registry-installed
/// clients may or may not surface a new version — so without this, improvements (notably to the
/// MCP tool descriptions) never reach anyone already installed.
/// </summary>
/// <remarks>
/// Deliberately cache-first: the notice shown now comes from disk, and the network call that
/// refreshes it runs in the background for the *next* invocation. That keeps the check off the
/// critical path entirely — no command is ever slower because of it, and a GitHub outage or an
/// offline machine costs nothing.
/// </remarks>
public static class UpdateChecker
{
    /// <summary>Set to any non-empty value to disable the check entirely.</summary>
    public const string DisableEnvVar = "INFOSLIDES_NO_UPDATE_CHECK";

    /// <summary>How long a cached result stays fresh.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    private const string ReleasesApiUrl = "https://api.github.com/repos/arnibj/InfoSlides.MCP/releases/latest";
    private const string ReleasesPageUrl = "https://github.com/arnibj/InfoSlides.MCP/releases/latest";
    private const string CacheFileName = "update-check.json";

    /// <summary>
    /// Returns a one-line notice when the cache holds a version newer than
    /// <paramref name="currentVersion"/>, otherwise null. Reads a local file only — never
    /// touches the network, so it is safe to call on every startup.
    /// </summary>
    /// <param name="currentVersion">The running build's version.</param>
    /// <param name="configDirectory">Directory holding the cache file.</param>
    /// <param name="getEnv">Environment lookup; defaults to the real environment.</param>
    /// <returns>The notice, or null when up to date, disabled, or nothing is cached.</returns>
    public static string? GetPendingNotice(
        string currentVersion, string configDirectory, Func<string, string?>? getEnv = null)
    {
        if (IsDisabled(getEnv))
        {
            return null;
        }

        var cached = ReadState(configDirectory)?.LatestVersion;
        return IsNewer(cached, currentVersion)
            ? $"A newer InfoSlides release is available: {currentVersion} -> {cached}. {ReleasesPageUrl}"
            : null;
    }

    /// <summary>
    /// Refreshes the cache when it is stale. Never throws and never blocks the caller's work —
    /// intended to be started and forgotten. If the process exits first the refresh is simply
    /// abandoned and retried next time.
    /// </summary>
    /// <param name="configDirectory">Directory holding the cache file.</param>
    /// <param name="httpClient">HTTP client to use; one is created when null.</param>
    /// <param name="getEnv">Environment lookup; defaults to the real environment.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task RefreshAsync(
        string configDirectory,
        HttpClient? httpClient = null,
        Func<string, string?>? getEnv = null,
        CancellationToken ct = default)
    {
        if (IsDisabled(getEnv) || IsFresh(ReadState(configDirectory)))
        {
            return;
        }

        var ownsClient = httpClient is null;
        var http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUrl);
            // GitHub rejects requests without a User-Agent.
            request.Headers.TryAddWithoutValidation("User-Agent", "infoslides-cli");
            request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");

            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var release = JsonSerializer.Deserialize(json, InfoSlidesJsonContext.Default.GitHubRelease);

            var latest = NormalizeVersion(release?.TagName);
            if (latest is not null)
            {
                WriteState(configDirectory, new UpdateCheckState(latest, DateTimeOffset.UtcNow));
            }
        }
        catch (Exception)
        {
            // A failed update check must never surface to the user or affect an exit code.
            // Offline, rate-limited, DNS-blocked and malformed-response all land here.
        }
        finally
        {
            if (ownsClient)
            {
                http.Dispose();
            }
        }
    }

    /// <summary>
    /// Compares two versions, tolerating a leading "v" and unparseable input.
    /// </summary>
    /// <param name="candidate">The possibly-newer version.</param>
    /// <param name="current">The running version.</param>
    /// <returns>True only when both parse and <paramref name="candidate"/> is strictly greater.</returns>
    public static bool IsNewer(string? candidate, string? current)
    {
        return Version.TryParse(NormalizeVersion(candidate), out var candidateVersion)
            && Version.TryParse(NormalizeVersion(current), out var currentVersion)
            && candidateVersion > currentVersion;
    }

    /// <summary>Full path of the cache file for a given config directory.</summary>
    /// <param name="configDirectory">The config directory.</param>
    /// <returns>The cache file path.</returns>
    public static string CachePath(string configDirectory) => Path.Combine(configDirectory, CacheFileName);

    // ── Internals ────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether the check is switched off — explicitly by the user, or implicitly on CI, where a
    /// per-build call to GitHub is pure noise and burns the unauthenticated rate limit.
    /// </summary>
    /// <param name="getEnv">Environment lookup.</param>
    /// <returns>True when no check should happen.</returns>
    private static bool IsDisabled(Func<string, string?>? getEnv)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        return !string.IsNullOrEmpty(getEnv(DisableEnvVar)) || !string.IsNullOrEmpty(getEnv("CI"));
    }

    private static bool IsFresh(UpdateCheckState? state) =>
        state is not null && DateTimeOffset.UtcNow - state.CheckedAt < CheckInterval;

    private static string? NormalizeVersion(string? raw)
    {
        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.StartsWith('v') || trimmed.StartsWith('V') ? trimmed[1..] : trimmed;
    }

    private static UpdateCheckState? ReadState(string configDirectory)
    {
        try
        {
            var path = CachePath(configDirectory);
            return File.Exists(path)
                ? JsonSerializer.Deserialize(File.ReadAllText(path), InfoSlidesJsonContext.Default.UpdateCheckState)
                : null;
        }
        catch (Exception)
        {
            // A corrupt or unreadable cache is equivalent to no cache.
            return null;
        }
    }

    private static void WriteState(string configDirectory, UpdateCheckState state)
    {
        try
        {
            Directory.CreateDirectory(configDirectory);
            var path = CachePath(configDirectory);
            var json = JsonSerializer.Serialize(state, InfoSlidesJsonContext.Default.UpdateCheckState);

            // Write-then-move: the refresh runs in the background and the process may exit
            // mid-write, which would otherwise leave a truncated file behind.
            var temp = path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception)
        {
            // Read-only home directory, full disk, races with another instance — none of which
            // are worth telling the user about.
        }
    }
}
