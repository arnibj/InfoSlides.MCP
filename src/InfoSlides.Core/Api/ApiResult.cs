using System.Text.Json;
using InfoSlides.Core.Models;

namespace InfoSlides.Core.Api;

/// <summary>
/// Successful API response: payload plus any non-fatal warnings (e.g. AspectMismatch) and, for a call
/// that changed something, the ready-made <c>undo</c> request (<c>POST /v1/undo</c> with a token).
/// </summary>
public sealed record ApiResult<T>(T Data, IReadOnlyList<ApiWarning> Warnings, JsonElement? Undo = null)
{
    public bool HasWarnings => Warnings.Count > 0;
}
