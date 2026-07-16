using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace InfoSlides.Core.Auth;

/// <summary>
/// OAuth local-loopback flow (contract §4.1): the backend brokers the identity provider; this
/// side only catches the one-time authorization code on http://localhost:{port}/callback and
/// exchanges it (with PKCE) for a session token. No IdP secrets ever live in the CLI.
/// </summary>
public static class LoopbackLoginFlow
{
    public const int DefaultPort = 5000;
    public static readonly int[] FallbackPorts = [5013, 53682];

    public sealed record LoginRequest(string AuthorizeUrl, string State, string CodeVerifier, int Port);

    public static LoginRequest Prepare(Uri apiUrl, string provider, int port = DefaultPort)
    {
        var state = RandomToken();
        var codeVerifier = RandomToken();
        var codeChallenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        var redirectUri = $"http://localhost:{port}/callback";
        var authorizeUrl = new UriBuilder(apiUrl)
        {
            Path = "/v1/auth/cli/start",
            Query = $"provider={Uri.EscapeDataString(provider)}&state={state}" +
                    $"&codeChallenge={codeChallenge}&redirectUri={Uri.EscapeDataString(redirectUri)}",
        }.Uri.ToString();
        return new LoginRequest(authorizeUrl, state, codeVerifier, port);
    }

    /// <summary>
    /// Listens on the loopback port for the backend's redirect and returns the authorization
    /// code. Serves a small confirmation page to the browser tab.
    /// </summary>
    public static async Task<string> WaitForCodeAsync(LoginRequest request, TimeSpan timeout, CancellationToken ct = default)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{request.Port}/callback/");
        listener.Start();
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(timeout);

            var context = await listener.GetContextAsync().WaitAsync(linked.Token).ConfigureAwait(false);
            var query = HttpUtility.ParseQueryString(context.Request.Url?.Query ?? "");
            var code = query["code"];
            var valid = code is { Length: > 0 } && query["state"] == request.State;

            await RespondAsync(context.Response, valid).ConfigureAwait(false);

            return valid
                ? code!
                : throw new InvalidOperationException(
                    "Login failed: the callback was missing a code or carried a mismatched state. Try again.");
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task RespondAsync(HttpListenerResponse response, bool success)
    {
        var html = success
            ? "<html><body style=\"font-family:system-ui;margin:4rem;text-align:center\">" +
              "<h2>Login complete</h2><p>You can close this tab and return to the terminal.</p></body></html>"
            : "<html><body style=\"font-family:system-ui;margin:4rem;text-align:center\">" +
              "<h2>Login failed</h2><p>Invalid callback. Return to the terminal and try again.</p></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = success ? 200 : 400;
        response.ContentType = "text/html; charset=utf-8";
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }

    private static string RandomToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
