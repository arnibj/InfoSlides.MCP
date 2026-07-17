using System.CommandLine;
using System.Net;
using InfoSlides.Core.Auth;
using InfoSlides.Core.Config;
using InfoSlides.Core.Models;

namespace InfoSlides.Cli.Commands;

internal static class LoginCommands
{
    public static Command Login()
    {
        var provider = new Option<string>("--provider")
        {
            Description = "Identity provider: google, microsoft or github.",
            DefaultValueFactory = _ => "google",
        };
        var port = new Option<int>("--port")
        {
            Description = $"Loopback callback port (default {LoopbackLoginFlow.DefaultPort}; " +
                          $"fallbacks registered with the backend: {string.Join(", ", LoopbackLoginFlow.FallbackPorts)}).",
            DefaultValueFactory = _ => LoopbackLoginFlow.DefaultPort,
        };
        var timeout = new Option<int>("--timeout-seconds")
        {
            Description = "How long to wait for the browser round-trip.",
            DefaultValueFactory = _ => 300,
        };

        var login = new Command("login", "Sign in via the browser (OAuth loopback) and store the session token.")
        {
            provider, port, timeout,
        };
        login.SetAction(async (parse, ct) =>
        {
            var providerValue = parse.GetValue(provider)!;
            if (providerValue is not ("google" or "microsoft" or "github"))
            {
                Console.Error.WriteLine("error: --provider must be google, microsoft or github.");
                return 2;
            }

            var settings = CliContext.Settings(parse);
            var request = LoopbackLoginFlow.Prepare(settings.ApiUrl, providerValue, parse.GetValue(port));

            Task<string> codeTask;
            try
            {
                codeTask = LoopbackLoginFlow.WaitForCodeAsync(
                    request, TimeSpan.FromSeconds(parse.GetValue(timeout)), ct);
            }
            catch (HttpListenerException exception)
            {
                Console.Error.WriteLine(
                    $"error: cannot listen on port {request.Port} ({exception.Message}). " +
                    $"On macOS, AirPlay Receiver often occupies port 5000 — retry with --port {LoopbackLoginFlow.FallbackPorts[0]}.");
                return 1;
            }

            Console.WriteLine("Opening your browser to sign in…");
            if (!BrowserLauncher.TryOpen(request.AuthorizeUrl))
            {
                Console.WriteLine("Could not open a browser automatically.");
            }

            Console.WriteLine($"If nothing opened, visit:\n  {request.AuthorizeUrl}");

            try
            {
                var code = await codeTask;
                var session = await CliContext.Client(parse)
                    .ExchangeCliCodeAsync(new CliCodeExchangeRequest(code, request.CodeVerifier), ct);

                new CredentialStore(settings.ConfigDirectory).SaveCredentials(new StoredCredentials(
                    SessionToken: session.Data.SessionToken,
                    ExpiresAt: session.Data.ExpiresAt,
                    TenantId: session.Data.TenantId,
                    Email: session.Data.Email));

                Console.WriteLine($"Logged in as {session.Data.Email} (tenant {session.Data.TenantId}).");
                return 0;
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("error: timed out waiting for the browser sign-in.");
                return 1;
            }
            catch (InvalidOperationException exception)
            {
                Console.Error.WriteLine($"error: {exception.Message}");
                return 1;
            }
            catch (Exception exception)
            {
                return CliContext.Fail(exception);
            }
        });
        return login;
    }

    public static Command Logout()
    {
        var logout = new Command("logout", "Delete locally stored credentials.");
        logout.SetAction(parse =>
        {
            new CredentialStore(CliContext.Settings(parse).ConfigDirectory).DeleteCredentials();
            Console.WriteLine("Logged out; stored credentials deleted.");
            return 0;
        });
        return logout;
    }
}
