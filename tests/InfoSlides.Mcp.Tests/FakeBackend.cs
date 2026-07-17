using System.Net;
using System.Net.Sockets;
using System.Text;

namespace InfoSlides.Mcp.Tests;

/// <summary>
/// Minimal in-process HTTP server standing in for the InfoSlides API, so the MCP server can be
/// exercised end-to-end over stdio without network access.
/// </summary>
public sealed class FakeBackend : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Dictionary<string, (int Status, string ContentType, byte[] Body)> _routes = new();

    public Uri BaseUrl { get; }

    public List<(string Method, string Path, string? Authorization)> Requests { get; } = [];

    public FakeBackend()
    {
        BaseUrl = new Uri($"http://127.0.0.1:{GetFreePort()}/");
        _listener.Prefixes.Add(BaseUrl.ToString());
        _listener.Start();
        _ = Task.Run(LoopAsync);
    }

    public void MapJson(string method, string path, string json, int status = 200) =>
        _routes[$"{method} {path}"] = (status, "application/json", Encoding.UTF8.GetBytes(json));

    public void MapBytes(string method, string path, byte[] body, string contentType) =>
        _routes[$"{method} {path}"] = (200, contentType, body);

    private async Task LoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception)
            {
                return;
            }

            var request = context.Request;
            lock (Requests)
            {
                Requests.Add((request.HttpMethod, request.Url!.AbsolutePath, request.Headers["Authorization"]));
            }

            var response = context.Response;
            if (_routes.TryGetValue($"{request.HttpMethod} {request.Url!.AbsolutePath}", out var route))
            {
                response.StatusCode = route.Status;
                response.ContentType = route.ContentType;
                await response.OutputStream.WriteAsync(route.Body);
            }
            else
            {
                response.StatusCode = 404;
                var body = Encoding.UTF8.GetBytes(
                    """{"error":{"code":"NotFound","message":"No such route in FakeBackend."}}""");
                response.ContentType = "application/json";
                await response.OutputStream.WriteAsync(body);
            }

            response.Close();
        }
    }

    private static int GetFreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    public void Dispose()
    {
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
