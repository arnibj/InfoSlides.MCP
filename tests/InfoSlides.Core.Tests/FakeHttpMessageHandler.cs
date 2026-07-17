using System.Net;
using System.Text;

namespace InfoSlides.Core.Tests;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    public sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Body,
        string? Authorization,
        string? IdempotencyKey);

    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<CapturedRequest> Requests { get; } = [];

    public void Enqueue(HttpStatusCode status, string body, string mediaType = "application/json") =>
        _responses.Enqueue(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        });

    public void EnqueueBytes(HttpStatusCode status, byte[] body, string mediaType) =>
        _responses.Enqueue(new HttpResponseMessage(status)
        {
            Content = new ByteArrayContent(body) { Headers = { ContentType = new(mediaType) } },
        });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(
            request.Method,
            request.RequestUri!,
            body,
            request.Headers.Authorization?.ToString(),
            request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.First() : null));

        return _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{}}""", Encoding.UTF8, "application/json"),
            };
    }
}
