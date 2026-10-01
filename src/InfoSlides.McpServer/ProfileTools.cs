// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InfoSlides.McpServer;

/// <summary>
/// The one tool the host provides itself: who is connected. ChatGPT uses a tool marked <c>openai/profile</c> to tell
/// connected accounts apart. It returns only an opaque, stable identifier and the display name, read from the access
/// token, and makes no API call.
/// </summary>
[McpServerToolType]
public sealed class ProfileTools(IHttpContextAccessor accessor)
{
    /// <summary>Returns an opaque id and the display name of the connected person.</summary>
    /// <returns>A JSON text result.</returns>
    [McpServerTool(Name = "get_user_profile", Title = "Get connected account", ReadOnly = true, Destructive = false, OpenWorld = false, Idempotent = true)]
    [Description("Returns a stable identifier and the display name of the person this connection acts for, so the assistant can tell connected accounts apart. Takes no arguments.")]
    public CallToolResult GetUserProfile()
    {
        var user = accessor.HttpContext?.User;
        var subject = user?.FindFirst("sub")?.Value ?? string.Empty;
        var issuer = user?.FindFirst("iss")?.Value ?? string.Empty;
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(issuer + "|" + subject)))[..24].ToLowerInvariant();
        var name = user?.FindFirst("name")?.Value;

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(new { id, name = string.IsNullOrWhiteSpace(name) ? "InfoSlides user" : name }) }],
        };
    }
}
