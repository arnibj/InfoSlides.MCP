using System.Text.Json;
using System.Text.Json.Serialization;
using InfoSlides.Core.Config;
using InfoSlides.Core.Models;

namespace InfoSlides.Core.Serialization;

/// <summary>
/// Source-generated serializer context covering every wire type. Native AOT builds have no
/// reflection-based serializer, so any type used in a request, response, or MCP tool signature
/// must be registered here (guarded by JsonContextCoverageTests).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(JsonElement?))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(ApiWarning))]
[JsonSerializable(typeof(List<ApiWarning>))]
[JsonSerializable(typeof(OkResult))]
[JsonSerializable(typeof(Resolution))]
[JsonSerializable(typeof(CreateTenantRequest))]
[JsonSerializable(typeof(CreateTenantResult))]
[JsonSerializable(typeof(TenantInfo))]
[JsonSerializable(typeof(CliCodeExchangeRequest))]
[JsonSerializable(typeof(SessionInfo))]
[JsonSerializable(typeof(SlideCondition))]
[JsonSerializable(typeof(List<SlideCondition>))]
[JsonSerializable(typeof(NewSlide))]
[JsonSerializable(typeof(List<NewSlide>))]
[JsonSerializable(typeof(Slide))]
[JsonSerializable(typeof(Slideshow))]
[JsonSerializable(typeof(List<Slideshow>))]
[JsonSerializable(typeof(CreateSlideshowRequest))]
[JsonSerializable(typeof(UpdateSlideshowRequest))]
[JsonSerializable(typeof(AddMediaSlideRequest))]
[JsonSerializable(typeof(UploadedMedia))]
[JsonSerializable(typeof(AddDynamicSlideRequest))]
[JsonSerializable(typeof(SetConditionsRequest))]
[JsonSerializable(typeof(Template))]
[JsonSerializable(typeof(List<Template>))]
[JsonSerializable(typeof(CreateTemplateRequest))]
[JsonSerializable(typeof(GalleryItem))]
[JsonSerializable(typeof(List<GalleryItem>))]
[JsonSerializable(typeof(Device))]
[JsonSerializable(typeof(List<Device>))]
[JsonSerializable(typeof(CreateDeviceRequest))]
[JsonSerializable(typeof(DeviceStatus))]
[JsonSerializable(typeof(AssignScheduleRequest))]
[JsonSerializable(typeof(Schedule))]
[JsonSerializable(typeof(StreamLink))]
[JsonSerializable(typeof(CreateApiKeyRequest))]
[JsonSerializable(typeof(CreateApiKeyResult))]
[JsonSerializable(typeof(ApiKeyInfo))]
[JsonSerializable(typeof(List<ApiKeyInfo>))]
[JsonSerializable(typeof(CheckoutLink))]
[JsonSerializable(typeof(StoredCredentials))]
[JsonSerializable(typeof(StoredConfig))]
public sealed partial class InfoSlidesJsonContext : JsonSerializerContext;
