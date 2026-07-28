using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using InfoSlides.Core.Config;
using InfoSlides.Core.Models;
using InfoSlides.Core.Serialization;
using InfoSlides.Core.Update;
using Xunit;

namespace InfoSlides.Core.Tests;

/// <summary>
/// Round-trips every wire model through the source-generated context. A type used by a tool or
/// endpoint but missing from InfoSlidesJsonContext would fail here long before an AOT publish.
/// </summary>
public sealed class JsonContextCoverageTests
{
    private static void RoundTrip<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.Serialize(value, typeInfo);
        var back = JsonSerializer.Deserialize(json, typeInfo);
        Assert.Equal(json, JsonSerializer.Serialize(back!, typeInfo));
    }

    [Fact]
    public void AllWireModels_RoundTripThroughContext()
    {
        var c = InfoSlidesJsonContext.Default;
        var now = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
        var resolution = new Resolution(1920, 1080);

        RoundTrip(resolution, c.Resolution);
        RoundTrip(new ApiWarning("AspectMismatch", "mismatch"), c.ApiWarning);
        RoundTrip(new List<ApiWarning> { new("A", "b") }, c.ListApiWarning);
        RoundTrip(OkResult.Instance, c.OkResult);

        RoundTrip(new CreateTenantRequest("Acme", "o@a.test"), c.CreateTenantRequest);
        RoundTrip(new CreateTenantResult("t1", "isk_admin_x", true), c.CreateTenantResult);
        RoundTrip(new TenantInfo("t1", "Acme", "o@a.test", true, "Premium",
            new DeviceQuota(1, 10), new KeyScope("admin", null)), c.TenantInfo);
        RoundTrip(new CliCodeExchangeRequest("code", "verifier"), c.CliCodeExchangeRequest);
        RoundTrip(new SessionInfo("tok", now, "t1", "o@a.test"), c.SessionInfo);

        var slide = new Slide("sl1", "https://cdn/x.png", null, 8, 0,
            [new SlideCondition("time", "08:00-11:00")]);
        RoundTrip(new SlideCondition("weekday", "sat,sun"), c.SlideCondition);
        RoundTrip(slide, c.Slide);
        RoundTrip(new Slideshow("s1", "Menu", resolution, [slide]), c.Slideshow);
        RoundTrip(new List<Slideshow> { new("s1", "Menu", resolution) }, c.ListSlideshow);
        RoundTrip(new CreateSlideshowRequest("Menu", resolution, [new NewSlide("https://cdn/x.png")]),
            c.CreateSlideshowRequest);
        RoundTrip(new UpdateSlideshowRequest("Menu 2", null, ["sl2", "sl1"]), c.UpdateSlideshowRequest);
        RoundTrip(new AddMediaSlideRequest("https://cdn/x.png", null, 10, 1), c.AddMediaSlideRequest);
        RoundTrip(new AddMediaSlideRequest(null, "asset1", 10, 1), c.AddMediaSlideRequest);
        RoundTrip(new UploadedMedia("asset1", "image", 800, 600), c.UploadedMedia);
        RoundTrip(new AddDynamicSlideRequest("tmpl1", 10, 1), c.AddDynamicSlideRequest);
        RoundTrip(new SetConditionsRequest([new SlideCondition("data_trigger", "sales_today > 1000000")]),
            c.SetConditionsRequest);

        using var sample = JsonDocument.Parse("""{"sales":0}""");
        RoundTrip(new Template("tp1", "Board", sample.RootElement.Clone(), "<div>{{sales}}</div>", "div{}"),
            c.Template);
        RoundTrip(new List<Template> { new("tp1", "Board") }, c.ListTemplate);
        RoundTrip(new CreateTemplateRequest("Board", "prompt", sample.RootElement.Clone()), c.CreateTemplateRequest);
        RoundTrip(new GalleryItem("g1", "Cafe", "desc", "https://cdn/p.png", resolution), c.GalleryItem);
        RoundTrip(new List<GalleryItem> { new("g1", "Cafe", null, null, null) }, c.ListGalleryItem);

        RoundTrip(new Device("d1", "Lobby", resolution), c.Device);
        RoundTrip(new List<Device> { new("d1", "Lobby", resolution) }, c.ListDevice);
        RoundTrip(new CreateDeviceRequest("Lobby", resolution), c.CreateDeviceRequest);
        RoundTrip(new DeviceStatus(true, now, new NowPlaying("s1", "sl1")), c.DeviceStatus);
        RoundTrip(new AssignScheduleRequest(["s1"]), c.AssignScheduleRequest);
        RoundTrip(new Schedule("d1", ["s1"]), c.Schedule);
        RoundTrip(new StreamLink("https://stream/x.m3u8", now), c.StreamLink);

        RoundTrip(new CreateApiKeyRequest("dataProvider", "crm-push", ["sl1"]), c.CreateApiKeyRequest);
        RoundTrip(new CreateApiKeyResult("k1", "isk_dp_x", "dataProvider", "crm-push"), c.CreateApiKeyResult);
        RoundTrip(new ApiKeyInfo("k1", "crm-push", "dataProvider", "isk_dp_x", ["sl1"], now, now, null),
            c.ApiKeyInfo);
        RoundTrip(new List<ApiKeyInfo>(), c.ListApiKeyInfo);
        RoundTrip(new CheckoutLink("https://checkout.paddle.com/x"), c.CheckoutLink);

        RoundTrip(new StoredCredentials("isk_admin_x", "tok", now, "t1", "o@a.test"), c.StoredCredentials);
        RoundTrip(new StoredConfig("https://api.local"), c.StoredConfig);

        RoundTrip(new UpdateCheckState("1.2.0", now), c.UpdateCheckState);
        RoundTrip(new GitHubRelease("v1.2.0"), c.GitHubRelease);
    }

    [Fact]
    public void GitHubRelease_ReadsTheApisSnakeCaseTagName()
    {
        // The context's camelCase policy would map TagName to "tagName"; GitHub sends "tag_name",
        // so the property needs its explicit JsonPropertyName. Without this the tag silently
        // deserialises to null and the update check never fires.
        var release = JsonSerializer.Deserialize(
            """{"tag_name":"v1.2.0","name":"1.2.0"}""", InfoSlidesJsonContext.Default.GitHubRelease);

        Assert.Equal("v1.2.0", release!.TagName);
    }

    [Fact]
    public void Serialization_UsesCamelCase_AndOmitsNulls()
    {
        var json = JsonSerializer.Serialize(
            new CreateTenantRequest("Acme", "o@a.test"), InfoSlidesJsonContext.Default.CreateTenantRequest);
        Assert.Equal("""{"tenantName":"Acme","ownerEmail":"o@a.test"}""", json);

        var sparse = JsonSerializer.Serialize(
            new Slide("sl1"), InfoSlidesJsonContext.Default.Slide);
        Assert.Equal("""{"id":"sl1"}""", sparse);
    }
}
