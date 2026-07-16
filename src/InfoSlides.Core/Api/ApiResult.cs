using InfoSlides.Core.Models;

namespace InfoSlides.Core.Api;

/// <summary>Successful API response: payload plus any non-fatal warnings (e.g. AspectMismatch).</summary>
public sealed record ApiResult<T>(T Data, IReadOnlyList<ApiWarning> Warnings)
{
    public bool HasWarnings => Warnings.Count > 0;
}
