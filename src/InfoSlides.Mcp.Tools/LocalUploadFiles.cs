// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

namespace InfoSlides.Mcp.Tools;

/// <summary>
/// Checks a local file path before an upload tool reads it. The type is checked first, before the disk is touched,
/// so a prompt cannot have a tool send an arbitrary file (an SSH key, a .env file) to InfoSlides or probe whether
/// one exists.
/// </summary>
public static class LocalUploadFiles
{
    /// <summary>Picture types: logos and media.</summary>
    public static readonly IReadOnlyList<string> Images = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg"];

    /// <summary>Picture, video and PDF types the media library takes.</summary>
    public static readonly IReadOnlyList<string> Media = [.. Images, ".mp4", ".mov", ".webm", ".pdf"];

    /// <summary>Slideshow file types.</summary>
    public static readonly IReadOnlyList<string> Slideshows = [".pptx", ".pdf"];

    /// <summary>Says what is wrong with a file an upload tool was given.</summary>
    /// <param name="filePath">The path the tool was given.</param>
    /// <param name="allowed">The extensions the tool accepts, lower case with the dot.</param>
    /// <returns>A message for the caller, or null when the file may be read.</returns>
    public static string? Problem(string filePath, IReadOnlyList<string> allowed)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!allowed.Contains(extension))
        {
            return $"Unsupported file type '{extension}'; expected {string.Join(", ", allowed)}.";
        }

        if (!File.Exists(filePath))
        {
            return $"File not found: {filePath}";
        }

        return null;
    }
}
