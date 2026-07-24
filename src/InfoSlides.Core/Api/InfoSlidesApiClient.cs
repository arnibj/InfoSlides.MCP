using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using InfoSlides.Core.Models;
using InfoSlides.Core.Serialization;

namespace InfoSlides.Core.Api;

/// <summary>
/// Typed client for the InfoSlides agent API (see API-CONTRACT.md). Shared by the CLI verbs and
/// the MCP tools; all JSON flows through the source-generated <see cref="InfoSlidesJsonContext"/>
/// so the client works under Native AOT.
/// </summary>
public sealed class InfoSlidesApiClient
{
    private static readonly MediaTypeHeaderValue JsonMediaType = new("application/json") { CharSet = "utf-8" };

    private readonly HttpClient _http;
    private readonly string? _credential;

    public InfoSlidesApiClient(HttpClient http, Uri baseUrl, string? credential)
    {
        _http = http;
        _http.BaseAddress = baseUrl;
        _credential = string.IsNullOrWhiteSpace(credential) ? null : credential;
    }

    public bool HasCredential => _credential is not null;

    // Tenants & auth

    public Task<ApiResult<CreateTenantResult>> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "/v1/tenants", Json(request, InfoSlidesJsonContext.Default.CreateTenantRequest),
            InfoSlidesJsonContext.Default.CreateTenantResult, anonymousAllowed: true, idempotent: true, ct);

    public Task<ApiResult<TenantInfo>> GetTenantInfoAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, "/v1/tenant", null, InfoSlidesJsonContext.Default.TenantInfo, false, false, ct);

    public Task<ApiResult<OkResult>> ResendVerificationEmailAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "/v1/auth/resend-verification", null, InfoSlidesJsonContext.Default.OkResult, false, false, ct);

    public Task<ApiResult<SessionInfo>> ExchangeCliCodeAsync(CliCodeExchangeRequest request, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "/v1/auth/cli/exchange", Json(request, InfoSlidesJsonContext.Default.CliCodeExchangeRequest),
            InfoSlidesJsonContext.Default.SessionInfo, anonymousAllowed: true, idempotent: false, ct);

    // Slideshows & slides

    public Task<ApiResult<Slideshow>> CreateSlideshowAsync(CreateSlideshowRequest request, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "/v1/slideshows", Json(request, InfoSlidesJsonContext.Default.CreateSlideshowRequest),
            InfoSlidesJsonContext.Default.Slideshow, false, idempotent: true, ct);

    public Task<ApiResult<List<Slideshow>>> ListSlideshowsAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, "/v1/slideshows", null, InfoSlidesJsonContext.Default.ListSlideshow, false, false, ct);

    public Task<ApiResult<Slideshow>> GetSlideshowAsync(string slideshowId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, $"/v1/slideshows/{Uri.EscapeDataString(slideshowId)}", null,
            InfoSlidesJsonContext.Default.Slideshow, false, false, ct);

    public Task<ApiResult<Slideshow>> UpdateSlideshowAsync(string slideshowId, UpdateSlideshowRequest request, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Patch, $"/v1/slideshows/{Uri.EscapeDataString(slideshowId)}",
            Json(request, InfoSlidesJsonContext.Default.UpdateSlideshowRequest),
            InfoSlidesJsonContext.Default.Slideshow, false, false, ct);

    public Task<ApiResult<Slideshow>> CloneSlideshowAsync(string slideshowId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, $"/v1/slideshows/{Uri.EscapeDataString(slideshowId)}/clone", null,
            InfoSlidesJsonContext.Default.Slideshow, false, idempotent: true, ct);

    /// <summary>
    /// Uploads a <c>.pptx</c> file and creates a slideshow from it (parses slide count/native
    /// resolution and queues thumbnail/stream rendering server-side — see
    /// <c>POST /v1/slideshows/pptx</c> in API-CONTRACT.md §4.2).
    /// </summary>
    /// <param name="fileContent">The file's content stream (left open/closed by the caller).</param>
    /// <param name="fileName">The original file name (must end in <c>.pptx</c>).</param>
    /// <param name="title">Optional display name; falls back to the file name server-side.</param>
    public Task<ApiResult<Slideshow>> UploadPptxAsync(
        Stream fileContent, string fileName, string? title, CancellationToken ct = default)
    {
        var content = new MultipartFormDataContent();
        var filePart = new StreamContent(fileContent);
        filePart.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.presentationml.presentation");
        content.Add(filePart, "file", fileName);
        if (!string.IsNullOrWhiteSpace(title))
        {
            content.Add(new StringContent(title), "title");
        }

        return SendAsync(
            HttpMethod.Post, "/v1/slideshows/pptx", content, InfoSlidesJsonContext.Default.Slideshow,
            anonymousAllowed: false, idempotent: true, ct);
    }

    /// <summary>
    /// Uploads a file directly into the tenant's media library (as opposed to
    /// <see cref="AddMediaSlideAsync"/>'s <c>mediaUrl</c>, which downloads from a public URL —
    /// see <c>POST /v1/media</c> in API-CONTRACT.md §4.2). Pass the returned
    /// <see cref="UploadedMedia.Id"/> as <c>mediaAssetId</c> to <see cref="AddMediaSlideRequest"/>.
    /// </summary>
    /// <param name="fileContent">The file's content stream (left open/closed by the caller).</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="contentType">Best-effort MIME type; the server re-derives the real type from
    /// the file's extension/signature.</param>
    public Task<ApiResult<UploadedMedia>> UploadMediaAsync(
        Stream fileContent, string fileName, string contentType, CancellationToken ct = default)
    {
        var content = new MultipartFormDataContent();
        var filePart = new StreamContent(fileContent);
        filePart.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        content.Add(filePart, "file", fileName);

        return SendAsync(
            HttpMethod.Post, "/v1/media", content, InfoSlidesJsonContext.Default.UploadedMedia,
            anonymousAllowed: false, idempotent: true, ct);
    }

    public Task<ApiResult<Slide>> AddMediaSlideAsync(string slideshowId, AddMediaSlideRequest request, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, $"/v1/slideshows/{Uri.EscapeDataString(slideshowId)}/slides",
            Json(request, InfoSlidesJsonContext.Default.AddMediaSlideRequest),
            InfoSlidesJsonContext.Default.Slide, false, idempotent: true, ct);

    /// <summary>
    /// Creates a template-driven dynamic slide in an existing slideshow (see
    /// <c>POST /v1/slideshows/{id}/slides/dynamic</c> in API-CONTRACT.md §4.2). The new slide
    /// starts with no content source and empty override data — push data via
    /// <see cref="UpdateSourceAsync"/>.
    /// </summary>
    public Task<ApiResult<Slide>> AddDynamicSlideAsync(string slideshowId, AddDynamicSlideRequest request, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, $"/v1/slideshows/{Uri.EscapeDataString(slideshowId)}/slides/dynamic",
            Json(request, InfoSlidesJsonContext.Default.AddDynamicSlideRequest),
            InfoSlidesJsonContext.Default.Slide, false, idempotent: true, ct);

    public Task<ApiResult<Slide>> SetSlideConditionsAsync(string slideId, IReadOnlyList<SlideCondition> conditions, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Put, $"/v1/slides/{Uri.EscapeDataString(slideId)}/conditions",
            Json(new SetConditionsRequest(conditions), InfoSlidesJsonContext.Default.SetConditionsRequest),
            InfoSlidesJsonContext.Default.Slide, false, false, ct);

    public Task<ApiResult<OkResult>> UpdateSourceAsync(string slideId, JsonElement payload, bool dryRun = false, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post,
            $"/v1/slides/{Uri.EscapeDataString(slideId)}/source{(dryRun ? "?dryRun=true" : "")}",
            Json(payload, InfoSlidesJsonContext.Default.JsonElement),
            InfoSlidesJsonContext.Default.OkResult, false, false, ct);

    public async Task<byte[]> GetSlidePreviewPngAsync(string slideId, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get,
            $"/v1/slides/{Uri.EscapeDataString(slideId)}/preview.png", null, anonymousAllowed: false, idempotent: false);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw await ReadErrorAsync(response, ct).ConfigureAwait(false);
        }

        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    // Gallery

    public Task<ApiResult<List<GalleryItem>>> ListGalleryAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, "/v1/gallery", null, InfoSlidesJsonContext.Default.ListGalleryItem, false, false, ct);

    public Task<ApiResult<Slideshow>> CloneGalleryItemAsync(string galleryItemId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, $"/v1/gallery/{Uri.EscapeDataString(galleryItemId)}/clone", null,
            InfoSlidesJsonContext.Default.Slideshow, false, idempotent: true, ct);

    // Templates

    public Task<ApiResult<Template>> CreateTemplateAsync(CreateTemplateRequest request, bool dryRun = false, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, $"/v1/templates{(dryRun ? "?dryRun=true" : "")}",
            Json(request, InfoSlidesJsonContext.Default.CreateTemplateRequest),
            InfoSlidesJsonContext.Default.Template, false, idempotent: true, ct);

    public Task<ApiResult<List<Template>>> ListTemplatesAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, "/v1/templates", null, InfoSlidesJsonContext.Default.ListTemplate, false, false, ct);

    public Task<ApiResult<Template>> GetTemplateAsync(string templateId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, $"/v1/templates/{Uri.EscapeDataString(templateId)}", null,
            InfoSlidesJsonContext.Default.Template, false, false, ct);

    // Devices, schedules & streams

    public Task<ApiResult<Device>> CreateDeviceAsync(CreateDeviceRequest request, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "/v1/devices", Json(request, InfoSlidesJsonContext.Default.CreateDeviceRequest),
            InfoSlidesJsonContext.Default.Device, false, idempotent: true, ct);

    public Task<ApiResult<List<Device>>> ListDevicesAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, "/v1/devices", null, InfoSlidesJsonContext.Default.ListDevice, false, false, ct);

    public Task<ApiResult<DeviceStatus>> GetDeviceStatusAsync(string deviceId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, $"/v1/devices/{Uri.EscapeDataString(deviceId)}/status", null,
            InfoSlidesJsonContext.Default.DeviceStatus, false, false, ct);

    public Task<ApiResult<Schedule>> AssignScheduleAsync(string deviceId, AssignScheduleRequest request, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, $"/v1/devices/{Uri.EscapeDataString(deviceId)}/schedule",
            Json(request, InfoSlidesJsonContext.Default.AssignScheduleRequest),
            InfoSlidesJsonContext.Default.Schedule, false, false, ct);

    public Task<ApiResult<StreamLink>> GetStreamLinkAsync(string deviceId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, $"/v1/devices/{Uri.EscapeDataString(deviceId)}/stream", null,
            InfoSlidesJsonContext.Default.StreamLink, false, false, ct);

    // API keys & billing

    public Task<ApiResult<CreateApiKeyResult>> CreateApiKeyAsync(CreateApiKeyRequest request, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "/v1/apikeys", Json(request, InfoSlidesJsonContext.Default.CreateApiKeyRequest),
            InfoSlidesJsonContext.Default.CreateApiKeyResult, false, idempotent: true, ct);

    public Task<ApiResult<List<ApiKeyInfo>>> ListApiKeysAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, "/v1/apikeys", null, InfoSlidesJsonContext.Default.ListApiKeyInfo, false, false, ct);

    public Task<ApiResult<OkResult>> RevokeApiKeyAsync(string keyId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, $"/v1/apikeys/{Uri.EscapeDataString(keyId)}", null,
            InfoSlidesJsonContext.Default.OkResult, false, false, ct);

    public Task<ApiResult<CheckoutLink>> CreateCheckoutAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "/v1/billing/checkout", null, InfoSlidesJsonContext.Default.CheckoutLink, false, false, ct);

    // Plumbing

    private static StringContent Json<TBody>(TBody body, JsonTypeInfo<TBody> typeInfo)
    {
        var content = new StringContent(JsonSerializer.Serialize(body, typeInfo), Encoding.UTF8);
        content.Headers.ContentType = JsonMediaType;
        return content;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, HttpContent? content, bool anonymousAllowed, bool idempotent)
    {
        if (_credential is null && !anonymousAllowed)
        {
            throw new MissingCredentialException();
        }

        var request = new HttpRequestMessage(method, path) { Content = content };
        if (_credential is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _credential);
        }

        if (idempotent)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        }

        return request;
    }

    private async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        HttpContent? content,
        JsonTypeInfo<T> responseType,
        bool anonymousAllowed,
        bool idempotent,
        CancellationToken ct)
    {
        using var request = CreateRequest(method, path, content, anonymousAllowed, idempotent);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw ParseError(response.StatusCode, body);
        }

        return ParseEnvelope(response.StatusCode, body, responseType);
    }

    private static ApiResult<T> ParseEnvelope<T>(System.Net.HttpStatusCode status, string body, JsonTypeInfo<T> responseType)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        }
        catch (JsonException e)
        {
            throw new ApiException("InvalidResponse", $"The server returned malformed JSON: {e.Message}", status);
        }

        using (doc)
        {
            var root = doc.RootElement;
            var warnings = ReadWarnings(root);

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("data", out var data) &&
                data.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                var payload = data.Deserialize(responseType)
                    ?? throw new ApiException("InvalidResponse", "The server returned an empty data payload.", status);
                return new ApiResult<T>(payload, warnings);
            }

            if (OkResult.Instance is T ok)
            {
                return new ApiResult<T>(ok, warnings);
            }

            throw new ApiException("InvalidResponse", "The server response is missing the data payload.", status);
        }
    }

    private static IReadOnlyList<ApiWarning> ReadWarnings(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array
            ? warnings.Deserialize(InfoSlidesJsonContext.Default.ListApiWarning) ?? []
            : [];

    private static async Task<ApiException> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return ParseError(response.StatusCode, body);
    }

    private static ApiException ParseError(System.Net.HttpStatusCode status, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object)
            {
                var code = error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String
                    ? c.GetString()! : "UnknownError";
                var message = error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                    ? m.GetString()! : $"The server returned HTTP {(int)status}.";
                JsonElement? details = error.TryGetProperty("details", out var d) ? d.Clone() : null;
                return new ApiException(code, message, status, details);
            }
        }
        catch (JsonException)
        {
            // Fall through to the generic error below.
        }

        return new ApiException("HttpError", $"The server returned HTTP {(int)status}.", status);
    }
}
