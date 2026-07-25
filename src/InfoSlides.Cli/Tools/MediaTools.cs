using System.ComponentModel;
using InfoSlides.Core.Api;
using InfoSlides.Core.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InfoSlides.Cli.Tools;

[McpServerToolType]
public sealed class MediaTools(InfoSlidesApiClient api)
{
    [McpServerTool(Name = "upload_media")]
    [Description("Upload a file from disk directly into the tenant's media library. Returns an asset id — " +
                 "pass it as mediaAssetId to add_media_slide to add it as a slide without a publicly " +
                 "reachable URL.")]
    public async Task<CallToolResult> UploadMedia(
        [Description("Absolute path to the file on disk.")] string filePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return ToolResults.ValidationError($"File not found: {filePath}");
        }

        await using var stream = File.OpenRead(filePath);
        return await ToolResults.Execute(
            () => api.UploadMediaAsync(stream, Path.GetFileName(filePath), ResolveContentType(filePath), ct),
            InfoSlidesJsonContext.Default.UploadedMedia);
    }

    /// <summary>
    /// Best-effort MIME type from the file extension — advisory only; the server re-derives the
    /// real type from the file's extension/signature.
    /// </summary>
    private static string ResolveContentType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".mp4" => "video/mp4",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream",
    };
}
