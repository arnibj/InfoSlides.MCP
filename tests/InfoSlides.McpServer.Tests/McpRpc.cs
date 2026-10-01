// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace InfoSlides.McpServer.Tests;

/// <summary>Helpers for talking JSON-RPC to the MCP endpoint over HTTP.</summary>
public static class McpRpc
{
    public static HttpRequestMessage Rpc(object body, string? bearer = null, string? protocolVersion = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        if (protocolVersion is not null)
        {
            request.Headers.Add("MCP-Protocol-Version", protocolVersion);
        }

        return request;
    }

    public static object Initialize(string version = "2025-11-25") => new
    {
        jsonrpc = "2.0",
        id = 1,
        method = "initialize",
        @params = new { protocolVersion = version, capabilities = new { }, clientInfo = new { name = "spike", version = "1" } },
    };

    /// <summary>Reads a JSON-RPC reply that may arrive as plain JSON or as a single SSE event.</summary>
    public static async Task<JsonElement> ReadRpcAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            var data = text.Split('\n').Where(l => l.StartsWith("data:", StringComparison.Ordinal)).Select(l => l[5..].Trim()).LastOrDefault(l => l.Length > 0)
                       ?? throw new InvalidOperationException("No data in event stream: " + text);
            text = data;
        }

        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
