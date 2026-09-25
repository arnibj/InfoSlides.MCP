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
        RoundTrip(new Slideshow("s1", "Menu", resolution, [slide], "Html", "Html"), c.Slideshow);
        RoundTrip(new List<Slideshow> { new("s1", "Menu", resolution) }, c.ListSlideshow);

        var rule = new SlideRule("hide", "any", [new SlideCondition("date", "2026-12-01..2026-12-26")], false, "Hidden in December");
        RoundTrip(rule, c.SlideRule);
        RoundTrip(new List<SlideRule> { rule, new("show", "all", null, true, "Shown on some slides") }, c.ListSlideRule);
        RoundTrip(new Screen("d1", "Front lobby", "https://infoslides.app/player/tok1"), c.Screen);
        RoundTrip(new List<Screen> { new("d1", "Front lobby", "https://infoslides.app/player/tok1") }, c.ListScreen);
        RoundTrip(new Ticker(false), c.Ticker);
        RoundTrip(new Ticker(true, ["src1"]), c.Ticker);
        RoundTrip(new Clock(true, "TopRight", false, "#000000", "#ffffff"), c.Clock);
        RoundTrip(new Source("src1", "BBC News", "RssFeed", now), c.Source);
        RoundTrip(new List<Source> { new("src1", "BBC News", "RssFeed") }, c.ListSource);
        RoundTrip(new Slide("sl3", null, "tmpl1", 10, 2, null, null, null, "dynamic", true,
            "https://infoslides.app/t.png", "Specials", [rule]), c.Slide);
        RoundTrip(new Slideshow("s1", "Lobby", resolution, [slide], null, "VideoStream", 15,
            new Ticker(false), new Clock(true, "TopLeft"), true, "Completed", now,
            [new Screen("d1", "Front lobby", "https://infoslides.app/player/tok1")], 1), c.Slideshow);
        RoundTrip(new UpdateSlideshowRequest(DefaultDurationSeconds: 15, Ticker: new Ticker(false),
            Clock: new Clock(false), Shared: true), c.UpdateSlideshowRequest);
        RoundTrip(new SetConditionsRequest([new SlideCondition("date", "..2026-12-26")], "hide", "any"),
            c.SetConditionsRequest);
        RoundTrip(new Device("d1", "Lobby", resolution, "s1", "Lobby show"), c.Device);

        using var patch = JsonDocument.Parse("""{"price":"1.990 kr"}""");
        RoundTrip(new UpdateSlideRequest(20, true, "tmpl1", "src1", patch.RootElement.Clone(),
            patch.RootElement.Clone(), patch.RootElement.Clone()), c.UpdateSlideRequest);
        RoundTrip(new UpdateSlideRequest(Hidden: true), c.UpdateSlideRequest);
        RoundTrip(new CreateSlideshowRequest("Menu", resolution, [new NewSlide("https://cdn/x.png")]),
            c.CreateSlideshowRequest);
        RoundTrip(new UpdateSlideshowRequest("Menu 2", null, ["sl2", "sl1"]), c.UpdateSlideshowRequest);
        RoundTrip(new UpdateSlideshowRequest(PlaybackMode: "inherit"), c.UpdateSlideshowRequest);
        RoundTrip(new AddMediaSlideRequest("https://cdn/x.png", null, 10, 1), c.AddMediaSlideRequest);
        RoundTrip(new AddMediaSlideRequest(null, "asset1", 10, 1), c.AddMediaSlideRequest);
        RoundTrip(new UploadedMedia("asset1", "image", 800, 600), c.UploadedMedia);
        RoundTrip(new AddDynamicSlideRequest("tmpl1", 10, 1), c.AddDynamicSlideRequest);
        RoundTrip(new AddDynamicSlideRequest("tmpl1", CreatePushKey: true), c.AddDynamicSlideRequest);
        RoundTrip(new Slide("sl2", TemplateId: "tmpl1", SourceId: "src1", PushKey: "isk_dp_x"), c.Slide);
        RoundTrip(new SetConditionsRequest([new SlideCondition("data_trigger", "sales_today > 1000000")]),
            c.SetConditionsRequest);

        using var sample = JsonDocument.Parse("""{"sales":0}""");
        RoundTrip(new Template("tp1", "Board", sample.RootElement.Clone(), "<div>{{sales}}</div>", "div{}"),
            c.Template);
        RoundTrip(new List<Template> { new("tp1", "Board") }, c.ListTemplate);
        RoundTrip(new CreateTemplateRequest("Board", "prompt", sample.RootElement.Clone()), c.CreateTemplateRequest);
        RoundTrip(new CreateTemplateRequest("Queue", Html: "<div>{{n}}</div>", Css: "", DataMode: "push"), c.CreateTemplateRequest);
        RoundTrip(new PushReceived(now), c.PushReceived);
        RoundTrip(new PushReceived(), c.PushReceived);
        RoundTrip(new PushSourceStatus("src1", "Queue data", now, 30, true, ["sl2"]), c.PushSourceStatus);
        RoundTrip(new CreatePushKeyRequest("queue system"), c.CreatePushKeyRequest);
        RoundTrip(new PushKey("k1", "queue system", "isk_dp_AbCd", "isk_dp_full"), c.PushKey);
        RoundTrip(new GalleryItem("g1", "Cafe", "desc", "https://cdn/p.png", resolution), c.GalleryItem);
        RoundTrip(new List<GalleryItem> { new("g1", "Cafe", null, null, null) }, c.ListGalleryItem);

        RoundTrip(new Device("d1", "Lobby", resolution), c.Device);
        RoundTrip(new List<Device> { new("d1", "Lobby", resolution) }, c.ListDevice);
        RoundTrip(new CreateDeviceRequest("Lobby", resolution), c.CreateDeviceRequest);
        RoundTrip(new DeviceStatus(true, now, new NowPlaying("s1", "sl1")), c.DeviceStatus);
        RoundTrip(new AssignScheduleRequest(["s1"]), c.AssignScheduleRequest);
        RoundTrip(new Schedule("d1", ["s1"]), c.Schedule);
        RoundTrip(new StreamLink("https://stream/x.m3u8", now, "https://infoslides.app/player/tok1", "VideoStream"), c.StreamLink);
        RoundTrip(new StreamLink(null, null, "https://infoslides.app/player/tok2", "Html"), c.StreamLink);

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
