// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using InfoSlides.Mcp.Tools;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>
/// An upload tool refuses a file that is not of its type before reading it or checking that it exists, so a prompt
/// cannot send a private key or a .env file to InfoSlides.
/// </summary>
public class LocalUploadFilesTests
{
    [Theory]
    [InlineData("id_rsa")]
    [InlineData(".env")]
    [InlineData("secrets.json")]
    [InlineData("deck.pptx.key")]
    public void ExistingFileOfAnotherType_IsRefusedByType(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-{name}");
        File.WriteAllText(path, "-----BEGIN OPENSSH PRIVATE KEY-----");
        try
        {
            var problem = LocalUploadFiles.Problem(path, LocalUploadFiles.Media);

            Assert.NotNull(problem);
            Assert.StartsWith("Unsupported file type", problem);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void MissingFileOfAnotherType_IsRefusedByType_NotByExistence()
    {
        var problem = LocalUploadFiles.Problem("/home/someone/.ssh/id_ed25519", LocalUploadFiles.Media);

        Assert.StartsWith("Unsupported file type", problem);
    }

    [Fact]
    public void MissingFileOfTheRightType_IsNotFound()
    {
        var problem = LocalUploadFiles.Problem(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png"), LocalUploadFiles.Media);

        Assert.StartsWith("File not found", problem);
    }

    [Fact]
    public void ExistingFileOfTheRightType_CaseInsensitive_MayBeRead()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.PNG");
        File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47]);
        try
        {
            Assert.Null(LocalUploadFiles.Problem(path, LocalUploadFiles.Media));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void VideoIsNotALogo()
    {
        Assert.StartsWith("Unsupported file type", LocalUploadFiles.Problem("clip.mp4", LocalUploadFiles.Images));
    }
}
