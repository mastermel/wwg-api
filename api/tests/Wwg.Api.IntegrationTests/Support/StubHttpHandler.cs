using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Wwg.Api.IntegrationTests.Support;

/// <summary>
/// Answers the app's outgoing HTTP requests (e.g. to a geocoding service) instead of the network,
/// and records them.
/// </summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    : HttpMessageHandler
{
    private readonly ConcurrentQueue<Uri> _requests = new();

    public IReadOnlyCollection<Uri> Requests => _requests;

    /// <summary>Answers every request with <paramref name="json"/> (200 unless <paramref name="status"/>).</summary>
    public static StubHttpHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        if (request.RequestUri is { } uri)
        {
            _requests.Enqueue(uri);
        }

        return Task.FromResult(respond(request));
    }
}
