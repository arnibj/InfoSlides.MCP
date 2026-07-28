using System.ComponentModel;
using InfoSlides.Core.Api;
using InfoSlides.Core.Models;
using InfoSlides.Core.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InfoSlides.Cli.Tools;

[McpServerToolType]
public sealed class ApiKeyTools(InfoSlidesApiClient api)
{
    [McpServerTool(Name = "create_api_key")]
    [Description("Issue a credential so another system or person can work with this workspace. Type " +
                 "'admin' grants full access — use it sparingly. Type 'dataProvider' creates a " +
                 "locked-down push-only key tied to named slides that can do nothing except feed those " +
                 "slides new values via update_source: the right choice when a till system, CRM, or " +
                 "script needs to keep one number on the screen current, because a leaked key cannot " +
                 "read or change anything else. The full key is shown once and never again — tell the " +
                 "user to store it somewhere safe.")]
    public Task<CallToolResult> CreateApiKey(
        [Description("Key type: 'admin' for full access, or 'dataProvider' for a push-only key.")] string type,
        [Description("What this key is for, e.g. 'till-system-lunch-price'.")] string name,
        [Description("Ids of the slides a push-only key may feed. Required for dataProvider keys.")]
        List<string>? slideIds = null,
        CancellationToken ct = default)
    {
        if (type is not ("admin" or "dataProvider"))
        {
            return Task.FromResult(ToolResults.ValidationError("Key type must be 'admin' or 'dataProvider'."));
        }

        if (type == "dataProvider" && (slideIds is null || slideIds.Count == 0))
        {
            return Task.FromResult(ToolResults.ValidationError(
                "dataProvider keys must be bound to at least one slide id."));
        }

        return ToolResults.Execute(
            () => api.CreateApiKeyAsync(new CreateApiKeyRequest(type, name, slideIds), ct),
            InfoSlidesJsonContext.Default.CreateApiKeyResult);
    }

    [McpServerTool(Name = "list_api_keys", ReadOnly = true)]
    [Description("Audit who and what can reach this workspace: every key with its scope, when it was " +
                 "created, and when it was last used. Only the opening characters of each key are " +
                 "shown, never the whole thing. Use it to spot unused keys worth revoking, or to find " +
                 "the id of a key to revoke.")]
    public Task<CallToolResult> ListApiKeys(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ListApiKeysAsync(ct), InfoSlidesJsonContext.Default.ListApiKeyInfo);

    [McpServerTool(Name = "revoke_api_key", Destructive = true)]
    [Description("Cut off a credential immediately — a leaked key, a system being decommissioned, " +
                 "someone who has left. Takes effect at once and cannot be undone; anything still " +
                 "using that key stops working, so confirm with the user before revoking.")]
    public Task<CallToolResult> RevokeApiKey(
        [Description("Id of the key to revoke (from list_api_keys).")] string keyId,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.RevokeApiKeyAsync(keyId, ct), InfoSlidesJsonContext.Default.OkResult);
}
