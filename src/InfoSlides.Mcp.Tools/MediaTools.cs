using System.ComponentModel;
using InfoSlides.Core.Api;
using InfoSlides.Core.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static InfoSlides.Mcp.Tools.ToolResults;

namespace InfoSlides.Mcp.Tools;

[McpServerToolType]
public sealed class MediaTools(InfoSlidesApiClient api)
{
    private static string Id(string id) => Uri.EscapeDataString(id);

    [McpServerTool(Name = "upload_media", Title = "Upload media file", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = false)]
    [Description("Send a picture or video from the user's own computer to their workspace — a photo of " +
                 "the specials board, a poster, a logo, a promo clip. Use this when the file is local " +
                 "and has no public web address. Returns an asset id; pass it to add_media_slide as " +
                 "mediaAssetId to put it on the screen. For a large file, check get_upload_limits first.")]
    public async Task<CallToolResult> UploadMedia(
        [Description("Absolute path to the image or video file on disk.")] string filePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return ValidationError($"File not found: {filePath}");
        }

        await using var stream = File.OpenRead(filePath);
        return await Execute(
            () => api.UploadMediaAsync(stream, Path.GetFileName(filePath), ResolveContentType(filePath), ct),
            InfoSlidesJsonContext.Default.UploadedMedia);
    }

    [McpServerTool(Name = "get_upload_limits", Title = "Get upload limits", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Check what an upload can be before sending one: storageUsedBytes, storageLimitBytes, " +
                 "storageFreeBytes, maxFileBytes (the largest upload; a file must be a little smaller) and " +
                 "acceptedFileTypes. Call it before a large upload, or after a quota or too-large error, instead " +
                 "of trying again.")]
    public Task<CallToolResult> GetUploadLimits(CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, "/v1/media/limits", ct: ct));

    [McpServerTool(Name = "list_media", Title = "List media library", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Browse the workspace's media library, newest first: id, name, fileType (image, video, " +
                 "document), sizeBytes, width, height, folderId, shared (a file another workspace shares, which " +
                 "cannot be changed here), sharedWithOthers and createdAt. Filter by folder, type or name; the " +
                 "answer also has total, totalPages and storage used and allowed. Use an id as mediaAssetId on " +
                 "add_media_slide.")]
    public Task<CallToolResult> ListMedia(
        [Description("Only files in this folder (ids from list_media_folders).")] string? folderId = null,
        [Description("image, video or document.")] string? type = null,
        [Description("Only files whose name contains this, any case.")] string? name = null,
        [Description("Page number, from 1.")] int? page = null,
        [Description("Items per page, 1 to 100 (default 50).")] int? pageSize = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, "/v1/media" + InfoSlidesApiClient.Query(
            ("folderId", folderId), ("type", type), ("name", name), ("page", page?.ToString()), ("pageSize", pageSize?.ToString())), ct: ct));

    [McpServerTool(Name = "get_media", Title = "Get media file status", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Check whether an uploaded file is ready to use: processingStatus (Ready, Queued, Processing or " +
                 "Failed), progressPercent while a video is being prepared, and error when it failed. Images and " +
                 "documents are always Ready. Poll at most every 5 seconds.")]
    public Task<CallToolResult> GetMedia(
        [Description("Id of the media file.")] string mediaAssetId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, $"/v1/media/{Id(mediaAssetId)}", ct: ct));

    [McpServerTool(Name = "update_media", Title = "Rename, move or share media file", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Rename a file in the media library, move it to a folder, or share it with other workspaces. " +
                 "Send only what should change. folderId moves it (moveToRoot puts it back at the top level); " +
                 "shared true lets other workspaces use it, false stops that. Only the workspace's own files can " +
                 "be changed.")]
    public Task<CallToolResult> UpdateMedia(
        [Description("Id of the media file.")] string mediaAssetId,
        [Description("New name, up to 200 characters.")] string? name = null,
        [Description("Folder to move it to (ids from list_media_folders).")] string? folderId = null,
        [Description("True to move it out of its folder to the top level.")] bool moveToRoot = false,
        [Description("True shares it with other workspaces, false stops sharing.")] bool? shared = null,
        CancellationToken ct = default)
    {
        if (name is null && folderId is null && !moveToRoot && shared is null)
        {
            return Task.FromResult(ValidationError("Give at least one of name, folderId, moveToRoot or shared."));
        }

        var body = Body(("name", name), ("folderId", folderId), ("shared", shared));
        if (moveToRoot)
        {
            // The API reads an explicit null folderId as "move to the root"; Body() drops nulls.
            body["folderId"] = null;
        }

        return Json(() => api.SendJsonAsync(HttpMethod.Patch, $"/v1/media/{Id(mediaAssetId)}", body, ct: ct));
    }

    [McpServerTool(Name = "delete_media", Title = "Delete media file", ReadOnly = false, Destructive = true, OpenWorld = false, Idempotent = true)]
    [Description("Delete a file from the media library for good and free its storage. Confirm with the person " +
                 "first. Refused while a slide uses the file: the error lists those slides (slideId, slideshowId, " +
                 "slideshowTitle), so change or remove them first.")]
    public Task<CallToolResult> DeleteMedia(
        [Description("Id of the media file.")] string mediaAssetId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Delete, $"/v1/media/{Id(mediaAssetId)}", ct: ct));

    [McpServerTool(Name = "list_media_folders", Title = "List media folders", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("List every media library folder as one flat list: id, name and parentFolderId (null at the " +
                 "top level). Find a folder by name here, then pass its id as folderId to list_media or update_media.")]
    public Task<CallToolResult> ListMediaFolders(CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Get, "/v1/media/folders", ct: ct));

    [McpServerTool(Name = "create_media_folder", Title = "Create media folder", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = false)]
    [Description("Create a folder in the media library, such as 'Menus', at the top level or inside another folder.")]
    public Task<CallToolResult> CreateMediaFolder(
        [Description("Folder name, 1 to 200 characters.")] string name,
        [Description("Folder to create it in; the top level when omitted.")] string? parentFolderId = null,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Post, "/v1/media/folders",
            Body(("name", name), ("parentFolderId", parentFolderId)), idempotent: true, ct));

    [McpServerTool(Name = "rename_media_folder", Title = "Rename media folder", ReadOnly = false, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Rename a media library folder.")]
    public Task<CallToolResult> RenameMediaFolder(
        [Description("Id of the folder.")] string folderId,
        [Description("New name, 1 to 200 characters.")] string name,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Patch, $"/v1/media/folders/{Id(folderId)}", Body(("name", name)), ct: ct));

    [McpServerTool(Name = "delete_media_folder", Title = "Delete media folder", ReadOnly = false, Destructive = true, OpenWorld = false, Idempotent = true)]
    [Description("Delete an empty media library folder. Refused while it still holds files or folders; move or " +
                 "delete those first.")]
    public Task<CallToolResult> DeleteMediaFolder(
        [Description("Id of the folder.")] string folderId,
        CancellationToken ct = default) =>
        Json(() => api.SendJsonAsync(HttpMethod.Delete, $"/v1/media/folders/{Id(folderId)}", ct: ct));

    /// <summary>
    /// Best-effort MIME type from the file extension — advisory only; the server re-derives the
    /// real type from the file's extension/signature.
    /// </summary>
    internal static string ResolveContentType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
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
