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
}
