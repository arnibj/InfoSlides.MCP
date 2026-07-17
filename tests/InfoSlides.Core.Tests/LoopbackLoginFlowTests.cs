using System.Net.Sockets;
using InfoSlides.Core.Auth;
using Xunit;

namespace InfoSlides.Core.Tests;

public sealed class LoopbackLoginFlowTests
{
    private static int GetFreePort()
    {
        using var probe = new TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        return ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
    }

    private static HttpClient DirectClient() =>
        new(new HttpClientHandler { UseProxy = false });

    [Fact]
    public void Prepare_BuildsAuthorizeUrlWithPkce()
    {
        var request = LoopbackLoginFlow.Prepare(new Uri("https://api.test.local"), "github", 5013);

        Assert.Contains("/v1/auth/cli/start", request.AuthorizeUrl);
        Assert.Contains("provider=github", request.AuthorizeUrl);
        Assert.Contains($"state={request.State}", request.AuthorizeUrl);
        Assert.Contains("codeChallenge=", request.AuthorizeUrl);
        Assert.Contains("redirectUri=http%3A%2F%2Flocalhost%3A5013%2Fcallback", request.AuthorizeUrl);
        Assert.NotEqual(request.State, request.CodeVerifier);
        // The verifier itself must never appear in the URL — only its S256 challenge.
        Assert.DoesNotContain(request.CodeVerifier, request.AuthorizeUrl);
    }

    [Fact]
    public async Task WaitForCode_ReturnsCode_OnValidCallback()
    {
        var request = LoopbackLoginFlow.Prepare(new Uri("https://api.test.local"), "google", GetFreePort());
        var waitTask = LoopbackLoginFlow.WaitForCodeAsync(request, TimeSpan.FromSeconds(30));

        using var browser = DirectClient();
        var response = await browser.GetAsync(
            $"http://localhost:{request.Port}/callback?code=one-time-code&state={request.State}");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("one-time-code", await waitTask);
    }

    [Fact]
    public async Task WaitForCode_Throws_OnStateMismatch()
    {
        var request = LoopbackLoginFlow.Prepare(new Uri("https://api.test.local"), "google", GetFreePort());
        var waitTask = LoopbackLoginFlow.WaitForCodeAsync(request, TimeSpan.FromSeconds(30));

        using var browser = DirectClient();
        var response = await browser.GetAsync(
            $"http://localhost:{request.Port}/callback?code=stolen&state=wrong");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => waitTask);
    }

    [Fact]
    public async Task WaitForCode_TimesOut_WhenNoCallbackArrives()
    {
        var request = LoopbackLoginFlow.Prepare(new Uri("https://api.test.local"), "google", GetFreePort());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LoopbackLoginFlow.WaitForCodeAsync(request, TimeSpan.FromMilliseconds(200)));
    }
}
