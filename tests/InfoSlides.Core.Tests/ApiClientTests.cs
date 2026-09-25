using System.Net;
using System.Text.Json;
using InfoSlides.Core.Api;
using InfoSlides.Core.Models;
using Xunit;

namespace InfoSlides.Core.Tests;

public sealed class ApiClientTests
{
    private static readonly Uri BaseUrl = new("https://api.test.local");

    private static (InfoSlidesApiClient Client, FakeHttpMessageHandler Handler) CreateClient(string? credential)
    {
        var handler = new FakeHttpMessageHandler();
        var client = new InfoSlidesApiClient(new HttpClient(handler), BaseUrl, credential);
        return (client, handler);
    }

    [Fact]
    public async Task CreateTenant_IsAnonymous_AndSendsIdempotencyKey()
    {
        var (client, handler) = CreateClient(credential: null);
        handler.Enqueue(HttpStatusCode.OK,
            """{"data":{"tenantId":"t1","apiKey":"isk_admin_abc","verificationEmailSent":true}}""");

        var result = await client.CreateTenantAsync(new CreateTenantRequest("Acme", "owner@acme.test"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/tenants", request.Uri.AbsolutePath);
        Assert.Null(request.Authorization);
        Assert.False(string.IsNullOrEmpty(request.IdempotencyKey));
        Assert.Contains("\"tenantName\":\"Acme\"", request.Body);
        Assert.Equal("isk_admin_abc", result.Data.ApiKey);
        Assert.True(result.Data.VerificationEmailSent);
    }

    [Fact]
    public async Task AssignSchedule_PassesAspectMismatchWarningThrough()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK,
            """
            {"data":{"deviceId":"d1","slideshowIds":["s1"]},
             "warnings":[{"code":"AspectMismatch","message":"1920x1080 content on a 1080x1920 device."}]}
            """);

        var result = await client.AssignScheduleAsync("d1", new AssignScheduleRequest(["s1"]));

        Assert.True(result.HasWarnings);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("AspectMismatch", warning.Code);
        Assert.Equal("Bearer isk_admin_abc", Assert.Single(handler.Requests).Authorization);
    }

    [Fact]
    public async Task EntitlementError_MapsToApiExceptionWithUpgradeUrl()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.Forbidden,
            """
            {"error":{"code":"EntitlementRequired","message":"Premium required.",
             "details":{"upgradeUrl":"https://checkout.paddle.com/x"}}}
            """);

        var exception = await Assert.ThrowsAsync<ApiException>(
            () => client.CreateTemplateAsync(new CreateTemplateRequest("Board", Prompt: "sales board")));

        Assert.Equal("EntitlementRequired", exception.Code);
        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        Assert.Equal("https://checkout.paddle.com/x", exception.UpgradeUrl);
    }

    [Fact]
    public async Task AuthenticatedCall_WithoutCredential_FailsFastWithoutHttp()
    {
        var (client, handler) = CreateClient(credential: null);

        await Assert.ThrowsAsync<MissingCredentialException>(() => client.GetTenantInfoAsync());

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UpdateSource_DryRun_AppendsQueryAndPostsRawPayload()
    {
        var (client, handler) = CreateClient("isk_dp_xyz");
        handler.Enqueue(HttpStatusCode.OK, """{"data":{}}""");
        using var payload = JsonDocument.Parse("""{"sales_today":1200000}""");

        await client.UpdateSourceAsync("slide-9", payload.RootElement, dryRun: true);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v1/slides/slide-9/source", request.Uri.AbsolutePath);
        Assert.Equal("?dryRun=true", request.Uri.Query);
        Assert.Equal("""{"sales_today":1200000}""", request.Body);
    }

    [Fact]
    public async Task PushSourceData_PostsRawPayloadToTheSource_AndReadsReceivedAt()
    {
        var (client, handler) = CreateClient("isk_dp_xyz");
        handler.Enqueue(HttpStatusCode.OK, """{"data":{"receivedAt":"2026-09-25T12:00:00Z"}}""");
        using var payload = JsonDocument.Parse("""{"nowServing":"A-142"}""");

        var result = await client.PushSourceDataAsync("src-1", payload.RootElement);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/sources/src-1/data", request.Uri.AbsolutePath);
        Assert.Equal("", request.Uri.Query);
        Assert.Equal("""{"nowServing":"A-142"}""", request.Body);
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero), result.Data!.ReceivedAt);
    }

    [Fact]
    public async Task PushSourceData_DryRun_AppendsQuery()
    {
        var (client, handler) = CreateClient("isk_dp_xyz");
        handler.Enqueue(HttpStatusCode.OK, """{"data":{"receivedAt":null}}""");
        using var payload = JsonDocument.Parse("{}");

        var result = await client.PushSourceDataAsync("src-1", payload.RootElement, dryRun: true);

        Assert.Equal("?dryRun=true", Assert.Single(handler.Requests).Uri.Query);
        Assert.Null(result.Data!.ReceivedAt);
    }

    [Fact]
    public async Task GetSourceStatus_ReadsTheStatus()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK,
            """{"data":{"id":"src-1","name":"Queue data","lastReceivedAt":null,"hideAfterMinutes":30,"isShowingData":false,"slideIds":["sl-1"]}}""");

        var result = await client.GetSourceStatusAsync("src-1");

        Assert.Equal("/v1/sources/src-1", Assert.Single(handler.Requests).Uri.AbsolutePath);
        Assert.False(result.Data!.IsShowingData);
        Assert.Equal(30, result.Data.HideAfterMinutes);
        Assert.Equal(["sl-1"], result.Data.SlideIds);
    }

    [Fact]
    public async Task CreateSourceKey_PostsTheName_AndReturnsTheKey()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.Created,
            """{"data":{"id":"k1","name":"queue system","keyPrefix":"isk_dp_AbCd","key":"isk_dp_full"}}""");

        var result = await client.CreateSourceKeyAsync("src-1", "queue system");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v1/sources/src-1/keys", request.Uri.AbsolutePath);
        Assert.Equal("""{"name":"queue system"}""", request.Body);
        Assert.Equal("isk_dp_full", result.Data!.Key);
    }

    [Fact]
    public async Task AddDynamicSlide_CreatePushKey_IsSent_AndSourceIdAndKeyAreRead()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.Created,
            """{"data":{"id":"sl-1","templateId":"t-1","durationSeconds":10,"position":0,"sourceId":"src-1","pushKey":"isk_dp_full"}}""");

        var result = await client.AddDynamicSlideAsync("show-1", new AddDynamicSlideRequest("t-1", CreatePushKey: true));

        Assert.Equal("""{"templateId":"t-1","createPushKey":true}""", Assert.Single(handler.Requests).Body);
        Assert.Equal("src-1", result.Data!.SourceId);
        Assert.Equal("isk_dp_full", result.Data.PushKey);
    }

    [Fact]
    public async Task RevokeApiKey_WithEmptyEnvelope_ReturnsOkResult()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK, "{}");

        var result = await client.RevokeApiKeyAsync("key-1");

        Assert.Same(OkResult.Instance, result.Data);
        Assert.Equal(HttpMethod.Delete, Assert.Single(handler.Requests).Method);
    }

    [Fact]
    public async Task ListSlideshows_ParsesListPayload()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK,
            """
            {"data":[
              {"id":"s1","title":"Menu","resolution":{"width":1920,"height":1080}},
              {"id":"s2","title":"Lobby","resolution":{"width":1080,"height":1920}}]}
            """);

        var result = await client.ListSlideshowsAsync();

        Assert.Equal(2, result.Data.Count);
        Assert.Equal(new Resolution(1080, 1920), result.Data[1].Resolution);
    }

    [Fact]
    public async Task GetSlidePreviewPng_ReturnsRawBytes()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        byte[] png = [0x89, 0x50, 0x4E, 0x47];
        handler.EnqueueBytes(HttpStatusCode.OK, png, "image/png");

        var result = await client.GetSlidePreviewPngAsync("slide-1");

        Assert.Equal(png, result);
        Assert.Equal("/v1/slides/slide-1/preview.png", Assert.Single(handler.Requests).Uri.AbsolutePath);
    }

    [Fact]
    public async Task MalformedSuccessBody_ThrowsInvalidResponse()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK, "not json");

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetTenantInfoAsync());

        Assert.Equal("InvalidResponse", exception.Code);
    }

    [Fact]
    public async Task NonEnvelopeError_MapsToGenericHttpError()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.BadGateway, "<html>bad gateway</html>");

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.ListDevicesAsync());

        Assert.Equal("HttpError", exception.Code);
        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
    }

    [Fact]
    public async Task UploadPptx_SendsMultipartWithFileAndTitle_AndIdempotencyKey()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.Created,
            """{"data":{"id":"s1","title":"Q3 Deck","resolution":{"width":1920,"height":1080}}}""");
        using var fileStream = new MemoryStream([1, 2, 3, 4]);

        var result = await client.UploadPptxAsync(fileStream, "deck.pptx", "Q3 Deck");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/slideshows/pptx", request.Uri.AbsolutePath);
        Assert.Contains("deck.pptx", request.Body);
        Assert.Contains("Q3 Deck", request.Body);
        Assert.False(string.IsNullOrEmpty(request.IdempotencyKey));
        Assert.Equal("Q3 Deck", result.Data.Title);
    }

    [Fact]
    public async Task UploadPptx_NoTitle_OmitsTitlePart()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.Created,
            """{"data":{"id":"s1","title":"deck","resolution":{"width":1920,"height":1080}}}""");
        using var fileStream = new MemoryStream([1]);

        await client.UploadPptxAsync(fileStream, "deck.pptx", title: null);

        var request = Assert.Single(handler.Requests);
        Assert.DoesNotContain("name=\"title\"", request.Body);
    }

    [Fact]
    public async Task UploadPptx_PdfFile_RoutesToPdfEndpoint()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.Created,
            """{"data":{"id":"s1","title":"Q3 Deck","resolution":{"width":1920,"height":1080}}}""");
        using var fileStream = new MemoryStream([1, 2, 3, 4]);

        var result = await client.UploadPptxAsync(fileStream, "deck.pdf", "Q3 Deck");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/slideshows/pdf", request.Uri.AbsolutePath);
        Assert.Contains("deck.pdf", request.Body);
        Assert.Contains("Q3 Deck", request.Body);
        Assert.False(string.IsNullOrEmpty(request.IdempotencyKey));
        Assert.Equal("Q3 Deck", result.Data.Title);
    }

    [Fact]
    public async Task UploadPptx_UnsupportedExtension_ThrowsWithoutSendingRequest()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        using var fileStream = new MemoryStream([1]);

        await Assert.ThrowsAsync<ArgumentException>(() => client.UploadPptxAsync(fileStream, "deck.key", null));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UploadMedia_SendsMultipartWithFile_AndReturnsAssetShape()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.Created,
            """{"data":{"id":"asset1","fileType":"image","width":800,"height":600}}""");
        using var fileStream = new MemoryStream([1, 2, 3]);

        var result = await client.UploadMediaAsync(fileStream, "photo.jpg", "image/jpeg");

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v1/media", request.Uri.AbsolutePath);
        Assert.Contains("photo.jpg", request.Body);
        Assert.False(string.IsNullOrEmpty(request.IdempotencyKey));
        Assert.Equal("asset1", result.Data.Id);
        Assert.Equal("image", result.Data.FileType);
        Assert.Equal(800, result.Data.Width);
    }

    [Fact]
    public async Task UpdateSlideshow_PlaybackModeOmitted_DoesNotSendTheField()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK,
            """{"data":{"id":"s1","title":"Menu 2","resolution":{"width":1920,"height":1080}}}""");

        await client.UpdateSlideshowAsync("s1", new UpdateSlideshowRequest(Title: "Menu 2"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.DoesNotContain("playbackMode", request.Body);
    }

    /// <summary>The HTMLP-15 clear-the-override path: the literal "inherit" sentinel, distinct from omitting the field above.</summary>
    [Fact]
    public async Task UpdateSlideshow_PlaybackModeInherit_SendsTheSentinelString_AndParsesResolvedFields()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK,
            """{"data":{"id":"s1","title":"Menu","resolution":{"width":1920,"height":1080},"playbackModeOverride":null,"effectivePlaybackMode":"VideoStream"}}""");

        var result = await client.UpdateSlideshowAsync("s1", new UpdateSlideshowRequest(PlaybackMode: "inherit"));

        var request = Assert.Single(handler.Requests);
        Assert.Contains("\"playbackMode\":\"inherit\"", request.Body);
        Assert.Null(result.Data.PlaybackModeOverride);
        Assert.Equal("VideoStream", result.Data.EffectivePlaybackMode);
    }

    [Fact]
    public async Task UpdateSlideshow_PlaybackModeHtml_SendsTheEnumNameVerbatim()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK,
            """{"data":{"id":"s1","title":"Menu","resolution":{"width":1920,"height":1080},"playbackModeOverride":"Html","effectivePlaybackMode":"Html"}}""");

        var result = await client.UpdateSlideshowAsync("s1", new UpdateSlideshowRequest(PlaybackMode: "Html"));

        Assert.Contains("\"playbackMode\":\"Html\"", Assert.Single(handler.Requests).Body);
        Assert.Equal("Html", result.Data.PlaybackModeOverride);
        Assert.Equal("Html", result.Data.EffectivePlaybackMode);
    }

    /// <summary>
    /// Regression for the pre-existing VideoStream contract (HTMLP-16): hlsUrl stays populated
    /// alongside the new playerUrl/playbackMode fields.
    /// </summary>
    [Fact]
    public async Task GetStreamLink_VideoStreamMode_ParsesHlsUrlAndPlayerUrl()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK,
            """{"data":{"hlsUrl":"https://stream/x.m3u8","expiresAt":null,"playerUrl":"https://infoslides.app/player/tok1","playbackMode":"VideoStream"}}""");

        var result = await client.GetStreamLinkAsync("d1");

        Assert.Equal("https://stream/x.m3u8", result.Data.HlsUrl);
        Assert.Equal("https://infoslides.app/player/tok1", result.Data.PlayerUrl);
        Assert.Equal("VideoStream", result.Data.PlaybackMode);
    }

    /// <summary>
    /// The HTMLP-16 mode-switch trap at the client layer: an Html-mode device's hlsUrl parses as
    /// null rather than the client coercing a missing/absent value into an empty string or throwing
    /// — the caller must be able to tell "no HLS URL" from "URL not yet fetched".
    /// </summary>
    [Fact]
    public async Task GetStreamLink_HtmlMode_ParsesNullHlsUrl_AndPlayerUrlStaysPopulated()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK,
            """
            {"data":{"hlsUrl":null,"expiresAt":null,"playerUrl":"https://infoslides.app/player/tok1","playbackMode":"Html"},
             "warnings":[{"code":"HtmlPlaybackMode","message":"no raw HLS URL in Html mode"}]}
            """);

        var result = await client.GetStreamLinkAsync("d1");

        Assert.Null(result.Data.HlsUrl);
        Assert.Equal("https://infoslides.app/player/tok1", result.Data.PlayerUrl);
        Assert.Equal("Html", result.Data.PlaybackMode);
        Assert.Contains(result.Warnings, w => w.Code == "HtmlPlaybackMode");
    }

    [Fact]
    public async Task AddDynamicSlide_SendsTemplateId_AndIdempotencyKey()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.Created,
            """{"data":{"id":"slide1","templateId":"tmpl1","durationSeconds":10,"position":0}}""");

        var result = await client.AddDynamicSlideAsync("show1", new AddDynamicSlideRequest("tmpl1", 10, 0));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/v1/slideshows/show1/slides/dynamic", request.Uri.AbsolutePath);
        Assert.Contains("\"templateId\":\"tmpl1\"", request.Body);
        Assert.False(string.IsNullOrEmpty(request.IdempotencyKey));
        Assert.Equal("slide1", result.Data.Id);
        Assert.Equal("tmpl1", result.Data.TemplateId);
    }

    [Fact]
    public async Task UpdateSlide_PatchesOnlyTheGivenFields_AndReadsTheNewSlideFields()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK,
            """{"data":{"id":"sl1","durationSeconds":15,"position":1,"type":"pptx","hidden":true,"thumbnailUrl":"https://x/t.png"}}""");

        var result = await client.UpdateSlideAsync("sl1", new UpdateSlideRequest(DurationSeconds: 15, Hidden: true));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("/v1/slides/sl1", request.Uri.AbsolutePath);
        Assert.Equal("""{"durationSeconds":15,"hidden":true}""", request.Body);
        Assert.Equal("pptx", result.Data.Type);
        Assert.True(result.Data.Hidden);
        Assert.Equal("https://x/t.png", result.Data.ThumbnailUrl);
    }

    [Fact]
    public async Task DeleteSlide_And_DeleteSlideshow_SendDeleteRequests()
    {
        var (client, handler) = CreateClient("isk_admin_abc");

        await client.DeleteSlideAsync("sl1");
        await client.DeleteSlideshowAsync("s1");

        Assert.Collection(handler.Requests,
            r =>
            {
                Assert.Equal(HttpMethod.Delete, r.Method);
                Assert.Equal("/v1/slides/sl1", r.Uri.AbsolutePath);
            },
            r =>
            {
                Assert.Equal(HttpMethod.Delete, r.Method);
                Assert.Equal("/v1/slideshows/s1", r.Uri.AbsolutePath);
            });
    }

    [Fact]
    public async Task DeleteSlideshow_OtherTenantsSlideshow_SurfacesNotFound()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.NotFound, """{"error":{"code":"NotFound","message":"Slideshow not found."}}""");

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.DeleteSlideshowAsync("s1"));

        Assert.Equal("NotFound", exception.Code);
    }

    [Fact]
    public async Task ReplaceSlideshowFile_PutsMultipartFile_AndPicksTheMediaTypeFromTheExtension()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK,
            """{"data":{"id":"s1","title":"Lobby","resolution":{"width":1920,"height":1080},"renderStatus":"Pending"}}""");
        await using var file = new MemoryStream([0x25, 0x50, 0x44, 0x46]);

        var result = await client.ReplaceSlideshowFileAsync("s1", file, "lobby.pdf");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("/v1/slideshows/s1/file", request.Uri.AbsolutePath);
        Assert.Contains("name=file", request.Body);
        Assert.Contains("lobby.pdf", request.Body);
        Assert.Contains("application/pdf", request.Body);
        Assert.Equal("Pending", result.Data.RenderStatus);
    }

    [Fact]
    public async Task ReplaceSlideshowFile_UnsupportedExtension_ThrowsWithoutHttp()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        await using var file = new MemoryStream([1]);

        await Assert.ThrowsAsync<ArgumentException>(() => client.ReplaceSlideshowFileAsync("s1", file, "notes.docx"));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SetSlideConditions_SendsModeAndMatchOnlyWhenGiven()
    {
        var (client, handler) = CreateClient("isk_admin_abc");

        await client.SetSlideConditionsAsync("sl1", [new SlideCondition("time", "08:00-11:00")]);
        await client.SetSlideConditionsAsync("sl1", [new SlideCondition("date", "2026-12-01..2026-12-26")], "hide", "any");

        Assert.Equal("""{"conditions":[{"type":"time","value":"08:00-11:00"}]}""", handler.Requests[0].Body);
        Assert.Contains("\"mode\":\"hide\"", handler.Requests[1].Body);
        Assert.Contains("\"match\":\"any\"", handler.Requests[1].Body);
    }

    [Fact]
    public async Task UpdateSlideshow_TickerClockAndDuration_AreNestedAsTheApiExpects()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK,
            """
            {"data":{"id":"s1","title":"Lobby","resolution":{"width":1920,"height":1080},"defaultDurationSeconds":15,
             "ticker":{"enabled":false},"clock":{"enabled":true,"position":"TopRight"},"shared":false,
             "renderStatus":"Completed","lastRenderedAt":"2026-09-25T12:00:00Z",
             "screens":[{"deviceId":"d1","name":"Front lobby","playerUrl":"https://infoslides.app/player/t"}]}}
            """);

        var result = await client.UpdateSlideshowAsync("s1", new UpdateSlideshowRequest(
            DefaultDurationSeconds: 15, Ticker: new Ticker(false), Clock: new Clock(true, "TopRight")));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(
            """{"defaultDurationSeconds":15,"ticker":{"enabled":false},"clock":{"enabled":true,"position":"TopRight"}}""",
            request.Body);
        Assert.Equal("Completed", result.Data.RenderStatus);
        Assert.Equal("Front lobby", Assert.Single(result.Data.Screens!).Name);
        Assert.False(result.Data.Ticker!.Enabled);
    }

    [Fact]
    public async Task ListSources_ReadsIdNameTypeAndLastFetched()
    {
        var (client, handler) = CreateClient("isk_admin_abc");
        handler.Enqueue(HttpStatusCode.OK,
            """{"data":[{"id":"src1","name":"BBC News","adapterType":"RssFeed","lastFetchedAt":"2026-09-25T12:00:00Z"},{"id":"src2","name":"Queue","adapterType":"Push"}]}""");

        var result = await client.ListSourcesAsync();

        Assert.Equal("/v1/sources", Assert.Single(handler.Requests).Uri.AbsolutePath);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal("RssFeed", result.Data[0].AdapterType);
        Assert.Null(result.Data[1].LastFetchedAt);
    }
}
