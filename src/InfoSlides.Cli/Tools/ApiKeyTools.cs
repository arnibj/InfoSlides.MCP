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
    [Description("Create a tenant API key. Type 'admin' grants full access; type 'dataProvider' creates a " +
                 "restricted push-only key hard-bound to specific slide ids that can ONLY call " +
                 "update_source (e.g. for a CRM pushing dashboard numbers). The full key is returned once " +
                 "— store it securely.")]
    public Task<CallToolResult> CreateApiKey(
        [Description("Key type: 'admin' or 'dataProvider'.")] string type,
        [Description("Display name, e.g. 'crm-sales-push'.")] string name,
        [Description("Slide ids the key is bound to. Required for dataProvider keys.")]
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
    [Description("List API keys (prefix only, never the full key) with scope, creation time and last use.")]
    public Task<CallToolResult> ListApiKeys(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ListApiKeysAsync(ct), InfoSlidesJsonContext.Default.ListApiKeyInfo);

    [McpServerTool(Name = "revoke_api_key", Destructive = true)]
    [Description("Revoke an API key immediately. Irreversible.")]
    public Task<CallToolResult> RevokeApiKey(
        [Description("Id of the key to revoke (from list_api_keys).")] string keyId,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.RevokeApiKeyAsync(keyId, ct), InfoSlidesJsonContext.Default.OkResult);
}
