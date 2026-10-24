// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.ComponentModel;
using InfoSlides.Core.Api;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static InfoSlides.Mcp.Tools.ToolResults;

namespace InfoSlides.Mcp.Tools;

/// <summary>
/// Platform news and knowledge (APX-77): the running release, upcoming-update notices, and the published help
/// articles, blog posts and release notes. All read-only public content, so every key and every role may call them.
/// </summary>
[McpServerToolType]
public sealed class PlatformTools(InfoSlidesApiClient api)
{
    /// <summary>Returns the running InfoSlides release, build, deploy time and the release notes for that release.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The API's <c>data</c> as the tool result.</returns>
    [McpServerTool(Name = "get_platform_version", Title = "Get InfoSlides version", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Which InfoSlides release is running: version (for example v1.10.2), build (changes with every " +
                 "deploy), deployedAt, and releaseNotes, the published release notes of this version as title, slug " +
                 "and url. Remember the version; when it has changed since you last looked, tell the person an update " +
                 "is live and read the release notes with get_article (type release-note). An empty releaseNotes list " +
                 "means the notes are not written yet, so check again later instead of guessing what changed.")]
    public Task<CallToolResult> GetPlatformVersion(CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, "/v1/platform/version", ct: ct));

    /// <summary>Returns the active platform notices, such as an upcoming update or maintenance window.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The API's <c>data</c> as the tool result.</returns>
    [McpServerTool(Name = "get_platform_notices", Title = "Get InfoSlides notices", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Notices from the InfoSlides team that are still active, newest first (an empty list when there " +
                 "are none). Each has kind (SystemUpdate for an update or maintenance window, NewFeature for news), " +
                 "title, message, postedAt, startsAt (when the update begins, or null) and expiresAt. When a " +
                 "SystemUpdate notice is active, tell the person what is coming and when, using startsAt.")]
    public Task<CallToolResult> GetPlatformNotices(CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, "/v1/platform/notices", ct: ct));

    /// <summary>Searches or lists the published help articles, blog posts and release notes.</summary>
    /// <param name="q">Free text; best match first. Without it, newest first.</param>
    /// <param name="type">help, blog or release-note.</param>
    /// <param name="since">Only articles published on or after this date.</param>
    /// <param name="releaseVersion">Only the release notes of this version.</param>
    /// <param name="page">Page number, from 1.</param>
    /// <param name="pageSize">Items per page, 1 to 100.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The API's <c>data</c> as the tool result.</returns>
    [McpServerTool(Name = "search_articles", Title = "Search InfoSlides help and news", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Search the InfoSlides help articles, blog posts and release notes. Search here before telling " +
                 "the person something cannot be done. Also finds news since a date (since) or the notes of one " +
                 "release (releaseVersion). Each item has type, title, slug, url (the public page), publishedAt, " +
                 "releaseVersion, appRoute (the page in the InfoSlides web app the article is about) and a short " +
                 "excerpt. Read a whole article with get_article.")]
    public Task<CallToolResult> SearchArticles(
        [Description("Free text, e.g. 'portrait screen'. Best match first; without it, newest first.")] string? q = null,
        [Description("help, blog or release-note; all three when omitted.")] string? type = null,
        [Description("Only articles published on or after this date, yyyy-MM-dd.")] string? since = null,
        [Description("Only the release notes of this version, e.g. v1.10.2.")] string? releaseVersion = null,
        [Description("Page number, from 1.")] int? page = null,
        [Description("Items per page, 1 to 100 (default 25).")] int? pageSize = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, "/v1/articles" + InfoSlidesApiClient.Query(
            ("q", q), ("type", type), ("since", since), ("releaseVersion", releaseVersion),
            ("page", page?.ToString()), ("pageSize", pageSize?.ToString())), ct: ct));

    /// <summary>Reads one published article as Markdown.</summary>
    /// <param name="type">help, blog or release-note.</param>
    /// <param name="slug">The article's slug from a search result.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The API's <c>data</c> as the tool result.</returns>
    [McpServerTool(Name = "get_article", Title = "Read InfoSlides article", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Read one InfoSlides help article, blog post or release note as Markdown, with the same fields " +
                 "as a search result. Pass on what matters to the person in your own words and link the url.")]
    public Task<CallToolResult> GetArticle(
        [Description("help, blog or release-note.")] string type,
        [Description("The slug from search_articles or get_platform_version.")] string slug,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, $"/v1/articles/{Uri.EscapeDataString(type)}/{Uri.EscapeDataString(slug)}", ct: ct));
}
