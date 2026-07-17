namespace InfoSlides.Core.Models;

/// <summary>Pixel resolution carried by both devices and slideshows (contract §4.2/§4.5).</summary>
public sealed record Resolution(int Width, int Height)
{
    public static Resolution Default => new(1920, 1080);

    public override string ToString() => $"{Width}x{Height}";
}

/// <summary>Non-fatal warning returned in the response envelope (e.g. AspectMismatch).</summary>
public sealed record ApiWarning(string Code, string Message);

/// <summary>Placeholder payload for endpoints whose success carries no data.</summary>
public sealed record OkResult
{
    public static OkResult Instance { get; } = new();
}
